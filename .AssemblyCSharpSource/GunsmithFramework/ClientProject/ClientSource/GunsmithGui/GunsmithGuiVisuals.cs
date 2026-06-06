namespace GunsmithFramework
{
    public static partial class GunsmithGui
    {
        private static void AddPartPrefabImage(RectTransform rectT, GunsmithGuiPart part, float fill)
        {
            if (TryGetPartSprite(part, out Sprite? sprite, out Color spriteColor))
            {
                _ = new GunsmithPartIcon(rectT, sprite!, spriteColor, fill);
                return;
            }

            if (TryGetPartVisual(part, out Texture2D? texture, out Rectangle sourceRect))
            {
                _ = new GunsmithPartIcon(rectT, texture!, sourceRect, Color.White, fill);
                return;
            }

            rectT.Parent = null;
        }

        private static bool TryGetPartVisual(GunsmithGuiPart part, out Texture2D? texture, out Rectangle sourceRect)
        {
            texture = null;
            sourceRect = Rectangle.Empty;
            if (string.IsNullOrWhiteSpace(part.VisualTexturePath) ||
                part.VisualSourceRect.Width <= 0 ||
                part.VisualSourceRect.Height <= 0)
            {
                return false;
            }

            try
            {
                string resolvedPath = GunsmithApi.ResolvePath(part.VisualTexturePath);
                texture = GunsmithApi.GetTexture(resolvedPath);
                sourceRect = Rectangle.Intersect(part.VisualSourceRect, new Rectangle(0, 0, texture.Width, texture.Height));
                if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
                {
                    return false;
                }

                string cacheKey = $"{resolvedPath}|{sourceRect.X},{sourceRect.Y},{sourceRect.Width},{sourceRect.Height}";
                if (!partIconSourceCache.TryGetValue(cacheKey, out Rectangle trimmedRect))
                {
                    trimmedRect = TrimTransparentBounds(texture, sourceRect);
                    partIconSourceCache[cacheKey] = trimmedRect;
                }

                sourceRect = trimmedRect;
                return sourceRect.Width > 0 && sourceRect.Height > 0;
            }
            catch (Exception ex)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to load part icon texture '{part.VisualTexturePath}': {ex.Message}");
                texture = null;
                sourceRect = Rectangle.Empty;
                return false;
            }
        }

        private static Rectangle TrimTransparentBounds(Texture2D texture, Rectangle rect)
        {
            Color[] pixels = new Color[rect.Width * rect.Height];
            texture.GetData(0, rect, pixels, 0, pixels.Length);

            int minX = rect.Width;
            int minY = rect.Height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < rect.Height; y++)
            {
                int rowOffset = y * rect.Width;
                for (int x = 0; x < rect.Width; x++)
                {
                    if (pixels[rowOffset + x].A <= 8) { continue; }
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return rect;
            }

            return new Rectangle(
                rect.X + minX,
                rect.Y + minY,
                maxX - minX + 1,
                maxY - minY + 1);
        }

        private static bool TryGetPartSprite(GunsmithGuiPart part, out Sprite? sprite, out Color color)
        {
            sprite = null;
            color = Color.White;
            ItemPrefab? prefab = PartPrefab(part);
            if (prefab == null)
            {
                return false;
            }

            if (prefab.InventoryIcon != null)
            {
                color = prefab.InventoryIconColor;
                sprite = prefab.InventoryIcon;
            }
            else
            {
                color = prefab.SpriteColor;
                sprite = prefab.Sprite;
            }

            if (color.A == 0)
            {
                color = Color.White;
            }
            return sprite != null;
        }

        private static ItemPrefab? PartPrefab(GunsmithGuiPart part)
        {
            if (string.IsNullOrWhiteSpace(part.ItemIdentifier))
            {
                return null;
            }

            Identifier identifier = part.ItemIdentifier.ToIdentifier();
            if (ItemPrefab.Prefabs.TryGet(identifier, out ItemPrefab? prefab))
            {
                return prefab;
            }

            return (MapEntityPrefab.FindByIdentifier(identifier) ??
                    MapEntityPrefab.FindByName(part.ItemIdentifier)) as ItemPrefab;
        }

        private static Rectangle CreatePreviewSourceRect(GunsmithSpriteState state, GunsmithPreviewSettings settings)
        {
            Rectangle bounds = state.ContentBounds;
            int padding = (int)Math.Round(settings.Padding);
            int x = bounds.X - padding + (int)Math.Round(settings.Offset.X);
            int y = bounds.Y - padding + (int)Math.Round(settings.Offset.Y);
            int width = Math.Max((int)Math.Ceiling((bounds.Width + padding * 2) / settings.Scale), 1);
            int height = Math.Max((int)Math.Ceiling((bounds.Height + padding * 2) / settings.Scale), 1);

            Rectangle sourceRect = new(x, y, width, height);
            Rectangle textureRect = new(0, 0, state.Texture.Width, state.Texture.Height);
            Rectangle clipped = Rectangle.Intersect(sourceRect, textureRect);
            return clipped.Width > 0 && clipped.Height > 0 ? clipped : textureRect;
        }

        private static bool TryCreatePreviewGeometry(Rectangle rect, GunsmithSpriteState state, GunsmithPreviewSettings settings, out Rectangle sourceRect, out Rectangle destination, out float scale)
        {
            sourceRect = CreatePreviewSourceRect(state, settings);
            return TryCreatePreviewGeometry(rect, state.Texture, sourceRect, out destination, out scale);
        }

        private static bool TryCreatePreviewGeometry(Rectangle rect, Texture2D texture, Rectangle sourceRect, out Rectangle destination, out float scale)
        {
            destination = Rectangle.Empty;
            scale = 1.0f;
            if (sourceRect.Width <= 0 || sourceRect.Height <= 0 || rect.Width <= 0 || rect.Height <= 0)
            {
                return false;
            }

            sourceRect = Rectangle.Intersect(sourceRect, new Rectangle(0, 0, texture.Width, texture.Height));
            if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
            {
                return false;
            }

            scale = Math.Min(rect.Width / (float)sourceRect.Width, rect.Height / (float)sourceRect.Height);
            int width = Math.Max((int)Math.Round(sourceRect.Width * scale), 1);
            int height = Math.Max((int)Math.Round(sourceRect.Height * scale), 1);
            destination = new Rectangle(rect.Center.X - width / 2, rect.Center.Y - height / 2, width, height);
            return true;
        }

        private sealed class GunsmithPreviewImage : GUIFrame
        {
            private readonly Item item;
            private readonly GunsmithPreviewSettings settings;

            public GunsmithPreviewImage(RectTransform rectT, Item item, GunsmithPreviewSettings settings)
                : base(rectT, style: null, color: Color.Transparent)
            {
                this.item = item;
                this.settings = settings;
            }

            public override void Draw(SpriteBatch spriteBatch)
            {
                if (!Visible || item.Removed || !GunsmithApi.TryGetValidState(item, out GunsmithSpriteState state))
                {
                    return;
                }

                base.Draw(spriteBatch);
                Texture2D texture = state.Texture;
                if (!TryCreatePreviewGeometry(Rect, state, settings, out Rectangle sourceRect, out Rectangle destination, out _))
                {
                    return;
                }

                spriteBatch.Draw(texture, destination, sourceRect, Color.White);
            }
        }

        private sealed class GunsmithPartIcon : GUIFrame
        {
            private readonly Sprite? sprite;
            private readonly Texture2D? texture;
            private readonly Rectangle sourceRect;
            private readonly Color iconColor;
            private readonly float fill;

            public GunsmithPartIcon(RectTransform rectT, Sprite sprite, Color color, float fill)
                : base(rectT, style: null, color: Color.Transparent)
            {
                this.sprite = sprite;
                texture = null;
                sourceRect = Rectangle.Empty;
                iconColor = color;
                this.fill = Math.Clamp(fill, 0.1f, 1.0f);
                CanBeFocused = false;
            }

            public GunsmithPartIcon(RectTransform rectT, Texture2D texture, Rectangle sourceRect, Color color, float fill)
                : base(rectT, style: null, color: Color.Transparent)
            {
                sprite = null;
                this.texture = texture;
                this.sourceRect = sourceRect;
                iconColor = color;
                this.fill = Math.Clamp(fill, 0.1f, 1.0f);
                CanBeFocused = false;
            }

            public override void Draw(SpriteBatch spriteBatch)
            {
                if (!Visible)
                {
                    return;
                }

                base.Draw(spriteBatch);
                if (Rect.Width <= 0 || Rect.Height <= 0)
                {
                    return;
                }

                if (sprite != null)
                {
                    if (sprite.size.X <= 0.0f || sprite.size.Y <= 0.0f)
                    {
                        return;
                    }

                    float scale = Math.Min(Rect.Width / sprite.size.X, Rect.Height / sprite.size.Y) * fill;
                    sprite.Draw(spriteBatch, Rect.Center.ToVector2(), iconColor, scale: scale);
                    return;
                }

                if (texture == null || sourceRect.Width <= 0 || sourceRect.Height <= 0)
                {
                    return;
                }

                float textureScale = Math.Min(Rect.Width / (float)sourceRect.Width, Rect.Height / (float)sourceRect.Height) * fill;
                int width = Math.Max((int)Math.Round(sourceRect.Width * textureScale), 1);
                int height = Math.Max((int)Math.Round(sourceRect.Height * textureScale), 1);
                Rectangle destination = new(Rect.Center.X - width / 2, Rect.Center.Y - height / 2, width, height);
                spriteBatch.Draw(texture, destination, sourceRect, iconColor);
            }
        }

        private sealed class GunsmithStatsText : GUIFrame
        {
            private const string Separator = " | ";

            private readonly List<GunsmithStatDisplay> stats;
            private readonly bool inline;

            public GunsmithStatsText(RectTransform rectT, GunsmithStats stats, bool inline)
                : base(rectT, style: null, color: Color.Transparent)
            {
                this.stats = FormatStats(stats);
                this.inline = inline;
                CanBeFocused = false;
            }

            public override void Draw(SpriteBatch spriteBatch)
            {
                if (!Visible || Rect.Width <= 0 || Rect.Height <= 0)
                {
                    return;
                }

                base.Draw(spriteBatch);

                var font = GUIStyle.SmallFont ?? GUIStyle.Font;
                if (font == null)
                {
                    return;
                }

                if (inline)
                {
                    DrawInline(spriteBatch, font);
                }
                else
                {
                    DrawVertical(spriteBatch, font);
                }
            }

            private void DrawInline(SpriteBatch spriteBatch, GUIFont font)
            {
                float lineHeight = Math.Max(font.MeasureString("Mg").Y + 1.0f, 1.0f);
                float x = Rect.X;
                float y = Rect.Y;
                bool hasTextOnLine = false;
                Vector2 separatorSize = font.MeasureString(Separator);

                foreach (GunsmithStatDisplay stat in stats)
                {
                    Vector2 textSize = font.MeasureString(stat.Text);
                    float requiredWidth = textSize.X + (hasTextOnLine ? separatorSize.X : 0.0f);
                    if (hasTextOnLine && x + requiredWidth > Rect.Right)
                    {
                        x = Rect.X;
                        y += lineHeight;
                        hasTextOnLine = false;
                    }
                    if (y + lineHeight > Rect.Bottom)
                    {
                        return;
                    }

                    if (hasTextOnLine)
                    {
                        GUI.DrawString(spriteBatch, new Vector2(x, y), Separator, Color.LightGray, Color.Black * 0.65f, 0, font);
                        x += separatorSize.X;
                    }

                    GUI.DrawString(spriteBatch, new Vector2(x, y), stat.Text, StatDisplayColor(stat), Color.Black * 0.65f, 0, font);
                    x += textSize.X;
                    hasTextOnLine = true;
                }
            }

            private void DrawVertical(SpriteBatch spriteBatch, GUIFont font)
            {
                float lineHeight = Math.Max(font.MeasureString("Mg").Y + 1.0f, 1.0f);
                float y = Rect.Y;

                foreach (GunsmithStatDisplay stat in stats)
                {
                    if (y + lineHeight > Rect.Bottom)
                    {
                        return;
                    }

                    GUI.DrawString(spriteBatch, new Vector2(Rect.X, y), stat.Text, StatDisplayColor(stat), Color.Black * 0.65f, 0, font);
                    y += lineHeight;
                }
            }
        }
    }
}


