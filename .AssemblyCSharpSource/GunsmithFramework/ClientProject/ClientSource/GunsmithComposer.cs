namespace GunsmithFramework
{
    public static partial class GunsmithApi
    {
        private static List<GunsmithLayer> ParseLayers(string layerSpec)
        {
            List<GunsmithLayer> layers = new();
            foreach (string layerText in layerSpec.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = layerText.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length < 7) { continue; }
                bool hasItemIdentifier = parts.Length >= 8;
                int texturePathIndex = hasItemIdentifier ? 3 : 2;
                int sourceRectIndex = hasItemIdentifier ? 4 : 3;
                int offsetIndex = hasItemIdentifier ? 5 : 4;
                int orderIndex = hasItemIdentifier ? 6 : 5;
                int scaleIndex = hasItemIdentifier ? 7 : 6;

                if (!TryParseRectangle(parts[sourceRectIndex], out Rectangle sourceRect)) { continue; }
                if (!TryParseVector2(parts[offsetIndex], out Vector2 offset)) { continue; }
                if (!int.TryParse(parts[orderIndex], out int order)) { order = 0; }
                float scale = 1.0f;
                if (!float.TryParse(parts[scaleIndex], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale) || scale <= 0)
                {
                    scale = 1.0f;
                }

                layers.Add(new GunsmithLayer
                {
                    SlotPath = parts[0],
                    PartId = parts[1],
                    ItemIdentifier = hasItemIdentifier ? parts[2] : string.Empty,
                    TexturePath = ResolvePath(parts[texturePathIndex]),
                    SourceRect = sourceRect,
                    Offset = offset,
                    Scale = scale,
                    Order = order
                });
            }

            return layers.OrderBy(layer => layer.Order).ToList();
        }

        private static Rectangle CalculateContentBounds(IReadOnlyList<GunsmithLayer> layers, int canvasWidth, int canvasHeight)
        {
            if (layers.Count == 0)
            {
                return new Rectangle(0, 0, Math.Max(canvasWidth, 1), Math.Max(canvasHeight, 1));
            }

            Rectangle bounds = layers[0].DrawBounds;
            for (int i = 1; i < layers.Count; i++)
            {
                bounds = Rectangle.Union(bounds, layers[i].DrawBounds);
            }

            Rectangle canvas = new(0, 0, Math.Max(canvasWidth, 1), Math.Max(canvasHeight, 1));
            Rectangle clipped = Rectangle.Intersect(bounds, canvas);
            return clipped.Width > 0 && clipped.Height > 0 ? clipped : canvas;
        }

        private static Texture2D ComposeTexture(IReadOnlyList<GunsmithLayer> layers, int width, int height)
        {
            GraphicsDevice graphics = graphicsDevice!;
            SpriteBatch batch = spriteBatch!;
            RenderTargetBinding[] previousTargets = graphics.GetRenderTargets();
            RenderTarget2D target = new(graphics, width, height, false, SurfaceFormat.Color, DepthFormat.None);

            graphics.SetRenderTarget(target);
            graphics.Clear(Color.Transparent);
            batch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null);
            foreach (GunsmithLayer layer in layers)
            {
                Texture2D texture = GetTexture(layer.TexturePath);
                batch.Draw(texture, layer.Offset, layer.SourceRect, Color.White, 0.0f, Vector2.Zero, layer.Scale, SpriteEffects.None, 0.0f);
            }
            batch.End();

            graphics.SetRenderTargets(previousTargets);
            return target;
        }

        private static Texture2D CreateInventoryTexture(Texture2D texture, Rectangle contentBounds, GunsmithInventorySettings settings)
        {
            GraphicsDevice graphics = graphicsDevice!;
            SpriteBatch batch = spriteBatch!;
            float scale = Math.Max(settings.Scale, 0.01f);
            float rotation = MathHelper.ToRadians(settings.RotationDegrees);
            float padding = Math.Max(settings.Padding, 0.0f);

            Rectangle sourceRect = Rectangle.Intersect(contentBounds, new Rectangle(0, 0, texture.Width, texture.Height));
            if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
            {
                sourceRect = CreateComposedSourceRect(texture);
            }

            float scaledWidth = sourceRect.Width * scale;
            float scaledHeight = sourceRect.Height * scale;
            float cos = Math.Abs(MathF.Cos(rotation));
            float sin = Math.Abs(MathF.Sin(rotation));
            int targetWidth = Math.Max((int)Math.Ceiling(scaledWidth * cos + scaledHeight * sin + padding * 2.0f), 1);
            int targetHeight = Math.Max((int)Math.Ceiling(scaledWidth * sin + scaledHeight * cos + padding * 2.0f), 1);

            RenderTargetBinding[] previousTargets = graphics.GetRenderTargets();
            RenderTarget2D target = new(graphics, targetWidth, targetHeight, false, SurfaceFormat.Color, DepthFormat.None);

            graphics.SetRenderTarget(target);
            graphics.Clear(Color.Transparent);
            batch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null);
            batch.Draw(
                texture,
                new Vector2(targetWidth * 0.5f, targetHeight * 0.5f),
                sourceRect,
                Color.White,
                rotation,
                new Vector2(sourceRect.Width * 0.5f, sourceRect.Height * 0.5f),
                scale,
                SpriteEffects.None,
                0.0f);
            batch.End();

            graphics.SetRenderTargets(previousTargets);
            return target;
        }

        private static Texture2D CreateWorldTexture(Texture2D texture, Rectangle contentBounds, GunsmithWorldSettings settings, Vector2 canvasOrigin, out Vector2 worldOrigin)
        {
            GraphicsDevice graphics = graphicsDevice!;
            SpriteBatch batch = spriteBatch!;
            float scale = Math.Max(settings.Scale, 0.01f);
            float rotation = MathHelper.ToRadians(settings.RotationDegrees);
            float padding = Math.Max(settings.Padding, 0.0f);
            Vector2 offset = settings.Offset;

            Rectangle sourceRect = Rectangle.Intersect(contentBounds, new Rectangle(0, 0, texture.Width, texture.Height));
            if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
            {
                sourceRect = CreateComposedSourceRect(texture);
            }

            float scaledWidth = sourceRect.Width * scale;
            float scaledHeight = sourceRect.Height * scale;
            float cos = Math.Abs(MathF.Cos(rotation));
            float sin = Math.Abs(MathF.Sin(rotation));
            int targetWidth = Math.Max((int)Math.Ceiling(scaledWidth * cos + scaledHeight * sin + padding * 2.0f + Math.Abs(offset.X) * 2.0f), 1);
            int targetHeight = Math.Max((int)Math.Ceiling(scaledWidth * sin + scaledHeight * cos + padding * 2.0f + Math.Abs(offset.Y) * 2.0f), 1);
            Vector2 targetCenter = new(targetWidth * 0.5f, targetHeight * 0.5f);
            Vector2 sourceOrigin = new(sourceRect.Width * 0.5f, sourceRect.Height * 0.5f);
            Vector2 originInSource = canvasOrigin - new Vector2(sourceRect.X, sourceRect.Y);
            Vector2 localOrigin = (originInSource - sourceOrigin) * scale;
            worldOrigin = targetCenter + Rotate(localOrigin, rotation);

            RenderTargetBinding[] previousTargets = graphics.GetRenderTargets();
            RenderTarget2D target = new(graphics, targetWidth, targetHeight, false, SurfaceFormat.Color, DepthFormat.None);

            graphics.SetRenderTarget(target);
            graphics.Clear(Color.Transparent);
            batch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null);
            batch.Draw(
                texture,
                targetCenter + offset,
                sourceRect,
                Color.White,
                rotation,
                sourceOrigin,
                scale,
                SpriteEffects.None,
                0.0f);
            batch.End();

            graphics.SetRenderTargets(previousTargets);
            return target;
        }

        private static Vector2 Rotate(Vector2 value, float radians)
        {
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);
            return new Vector2(
                value.X * cos - value.Y * sin,
                value.X * sin + value.Y * cos);
        }

        internal static Texture2D GetTexture(string path)
        {
            return textureCache.GetOrAdd(path, static p =>
            {
                using FileStream stream = File.OpenRead(p);
                return Texture2D.FromStream(graphicsDevice!, stream);
            });
        }

        private static Sprite? CreateWorldSprite(Sprite? original, Texture2D texture, Vector2 origin)
        {
            if (original == null) { return null; }

            Rectangle sourceRect = CreateComposedSourceRect(texture);
            Sprite clone = new(original)
            {
                SourceRect = sourceRect,
                Origin = origin,
                RelativeOrigin = new Vector2(origin.X / sourceRect.Width, origin.Y / sourceRect.Height),
                RelativeSize = Vector2.One,
                Depth = original.Depth,
                SourceElement = original.SourceElement,
                EntityIdentifier = original.EntityIdentifier,
                FilePath = original.FilePath
            };
            clone.texture = texture;
            return clone;
        }

        private static Sprite? CreateInventorySprite(Sprite? original, Texture2D texture)
        {
            if (original == null) { return null; }

            Rectangle sourceRect = CreateComposedSourceRect(texture);
            Sprite clone = new(original)
            {
                SourceRect = sourceRect,
                Origin = new Vector2(sourceRect.Width * 0.5f, sourceRect.Height * 0.5f),
                RelativeOrigin = new Vector2(0.5f, 0.5f),
                RelativeSize = Vector2.One,
                Depth = original.Depth,
                SourceElement = original.SourceElement,
                EntityIdentifier = original.EntityIdentifier,
                FilePath = original.FilePath
            };
            clone.texture = texture;
            return clone;
        }

        private static Rectangle CreateComposedSourceRect(Texture2D texture)
            => new(0, 0, Math.Max(texture.Width, 1), Math.Max(texture.Height, 1));

        private static GunsmithInventorySettings ParseInventorySettings(string value)
        {
            GunsmithInventorySettings settings = GunsmithInventorySettings.Default;
            if (string.IsNullOrWhiteSpace(value)) { return settings; }

            foreach (string entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                {
                    continue;
                }

                settings = parts[0] switch
                {
                    "scale" when parsed > 0.0f => settings with { Scale = parsed },
                    "rotation" => settings with { RotationDegrees = parsed },
                    "padding" when parsed >= 0.0f => settings with { Padding = parsed },
                    _ => settings
                };
            }

            return settings;
        }

        private static GunsmithWorldSettings ParseWorldSettings(string value)
        {
            GunsmithWorldSettings settings = GunsmithWorldSettings.Default;
            if (string.IsNullOrWhiteSpace(value)) { return settings; }

            foreach (string entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                {
                    continue;
                }

                settings = parts[0] switch
                {
                    "scale" when parsed > 0.0f => settings with { Scale = parsed },
                    "rotation" => settings with { RotationDegrees = parsed },
                    "padding" when parsed >= 0.0f => settings with { Padding = parsed },
                    "offsetX" => settings with { Offset = new Vector2(parsed, settings.Offset.Y) },
                    "offsetY" => settings with { Offset = new Vector2(settings.Offset.X, parsed) },
                    _ => settings
                };
            }

            return settings;
        }

        private static string BuildSpriteSignature(string layerSpec, string inventorySpec, string worldSpec, int width, int height)
            => string.Concat(layerSpec, "|inventory:", inventorySpec, "|world:", worldSpec, "|canvas:", width, "x", height);

        internal static string ResolvePath(string path)
        {
            string resolved = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            if (GunsmithFramework.Package != null)
            {
                resolved = resolved.Replace("%ModDir%", GunsmithFramework.Package.Dir, StringComparison.OrdinalIgnoreCase);
            }
            return resolved;
        }

        internal static bool TryParseRectangle(string value, out Rectangle rectangle)
        {
            rectangle = default;
            string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 4) { return false; }
            if (!int.TryParse(parts[0], out int x)) { return false; }
            if (!int.TryParse(parts[1], out int y)) { return false; }
            if (!int.TryParse(parts[2], out int width)) { return false; }
            if (!int.TryParse(parts[3], out int height)) { return false; }
            rectangle = new Rectangle(x, y, width, height);
            return true;
        }

        private static bool TryParseVector2(string value, out Vector2 vector)
        {
            vector = default;
            string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 2) { return false; }
            if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)) { return false; }
            if (!float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)) { return false; }
            vector = new Vector2(x, y);
            return true;
        }
    }
}
