namespace GunsmithFramework
{
    public static partial class GunsmithApi
    {
        internal const string ReloadDepthBridgeHookName = "GunsmithFrameworkResolveAttachmentDepth";

        private static readonly ConcurrentDictionary<Item, GunsmithSpriteState> spriteStates = new();
        private static readonly ConcurrentDictionary<string, Texture2D> textureCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> WeaponTags = new(StringComparer.OrdinalIgnoreCase);
        private static GraphicsDevice? graphicsDevice;
        private static SpriteBatch? spriteBatch;

        public static bool IsReady => graphicsDevice != null && spriteBatch != null;
        internal static bool HasManagedRuntimeItems => GunsmithRuntimeStates.HasManagedRuntimeItems;
        internal static bool HasAnySpriteState => !spriteStates.IsEmpty;

        public static void Initialize(GraphicsDevice graphics)
        {
            graphicsDevice = graphics;
            spriteBatch = new SpriteBatch(graphics);
        }

        public static void RegisterWeaponTags(string tagSpec)
        {
            foreach (string rawTag in tagSpec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(rawTag))
                {
                    WeaponTags.Add(rawTag);
                }
            }
        }

        private static bool HasRegisteredWeaponTag(Item item)
            => WeaponTags.Count > 0 && WeaponTags.Any(tag => item.HasTag(tag));

        public static bool ApplyFromLua(Item item, string signature, string layerSpec, string inventorySpec, string worldSpec, string statsSpec, string managedItemSpec, int width, int height)
        {
            if (!IsReady || item == null || item.Removed) { return false; }
            if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(layerSpec)) { return false; }

            GunsmithRuntimeState runtimeState = GunsmithRuntimeStates.CreateState(signature, statsSpec, managedItemSpec);

            string spriteSignature = BuildSpriteSignature(layerSpec, inventorySpec, worldSpec, width, height);
            if (spriteStates.TryGetValue(item, out GunsmithSpriteState? existing) && existing.Signature == spriteSignature)
            {
                SetRuntimeState(item, runtimeState);
                ApplyState(item, existing);
                return true;
            }

            List<GunsmithLayer> layers = ParseLayers(layerSpec);
            if (layers.Count == 0) { return false; }
            GunsmithInventorySettings inventorySettings = ParseInventorySettings(inventorySpec);
            GunsmithWorldSettings worldSettings = ParseWorldSettings(worldSpec);

            bool shouldOwnWorldSprite = HasRegisteredWeaponTag(item);
            bool shouldReplaceActiveSprite =
                shouldOwnWorldSprite ||
                ReferenceEquals(item.activeSprite, item.Prefab.Sprite) ||
                (existing != null && ReferenceEquals(item.activeSprite, existing.WorldSprite));

            Texture2D texture;
            try
            {
                texture = ComposeTexture(layers, Math.Max(width, 1), Math.Max(height, 1));
            }
            catch (Exception ex)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to compose runtime texture: {ex.Message}");
                return false;
            }

            Rectangle contentBounds = CalculateContentBounds(layers, Math.Max(width, 1), Math.Max(height, 1));
            Sprite? originalWorldSprite = item.Prefab.Sprite;
            Vector2 canvasOrigin = originalWorldSprite != null
                ? new Vector2(texture.Width * originalWorldSprite.RelativeOrigin.X, texture.Height * originalWorldSprite.RelativeOrigin.Y)
                : new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            Texture2D worldTexture = CreateWorldTexture(texture, contentBounds, worldSettings, canvasOrigin, out Vector2 worldOrigin);
            Texture2D inventoryTexture = CreateInventoryTexture(texture, contentBounds, inventorySettings);
            Sprite? worldSprite = CreateWorldSprite(originalWorldSprite, worldTexture, worldOrigin);
            Sprite? inventorySprite = CreateInventorySprite(item.Prefab.InventoryIcon ?? item.Prefab.Sprite, inventoryTexture);
            if (worldSprite == null || inventorySprite == null)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to create runtime sprites for '{item.Prefab.Identifier.Value}'.");
                texture.Dispose();
                worldTexture.Dispose();
                inventoryTexture.Dispose();
                return false;
            }

            GunsmithSpriteState state = new()
            {
                Signature = spriteSignature,
                Texture = texture,
                WorldTexture = worldTexture,
                InventoryTexture = inventoryTexture,
                WorldSprite = worldSprite,
                InventorySprite = inventorySprite,
                ContentBounds = contentBounds,
                Layers = layers,
                CanvasOrigin = canvasOrigin,
                WorldSettings = worldSettings
            };

            spriteStates[item] = state;
            SetRuntimeState(item, runtimeState);
            ApplyState(item, state, shouldReplaceActiveSprite);

            if (existing != null && ReferenceEquals(item.activeSprite, existing.WorldSprite))
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to apply updated world sprite for '{item.Prefab.Identifier.Value}'.");
            }

            if (existing != null)
            {
                existing.Texture.Dispose();
                existing.WorldTexture.Dispose();
                existing.InventoryTexture.Dispose();
            }

            return true;
        }

        public static void ClearFromLua(Item item)
        {
            if (item == null) { return; }
            RemoveState(item);
            RestoreVanillaSprite(item);
        }

        internal static bool TryGetValidState(Item item, out GunsmithSpriteState state)
        {
            if (spriteStates.TryGetValue(item, out state!) && IsStateUsable(state))
            {
                return true;
            }

            if (state != null)
            {
                ClearFromLua(item);
            }
            return false;
        }

        private static void SetRuntimeState(Item item, GunsmithRuntimeState state)
            => GunsmithRuntimeStates.Set(item, state);

        private static void RemoveRuntimeState(Item item)
            => GunsmithRuntimeStates.Remove(item);

        internal static bool TryCanvasPointToItemLocal(Item item, Vector2 canvasPoint, out Vector2 localPoint)
        {
            localPoint = Vector2.Zero;
            if (!TryGetValidState(item, out GunsmithSpriteState state))
            {
                return false;
            }

            Vector2 delta = (canvasPoint - state.CanvasOrigin) * Math.Max(state.WorldSettings.Scale, 0.01f);
            Vector2 textureOffset = state.WorldSettings.Offset + Rotate(delta, MathHelper.ToRadians(state.WorldSettings.RotationDegrees));
            localPoint = new Vector2(textureOffset.X, -textureOffset.Y);
            return true;
        }

        internal static bool TryGetSpriteSignature(Item item, out string signature)
        {
            signature = string.Empty;
            if (!TryGetValidState(item, out GunsmithSpriteState state))
            {
                return false;
            }

            signature = state.Signature;
            return true;
        }

        public static bool TryGetAttachmentDepth(Item weaponItem, Item attachmentItem, out float depth)
        {
            depth = 0.0f;
            if (weaponItem == null || attachmentItem == null || weaponItem.Removed || attachmentItem.Removed)
            {
                return false;
            }

            if (TryGetAttachmentDepthLocal(weaponItem, attachmentItem, out depth))
            {
                return true;
            }

            return !GunsmithLuaHooks.HasRegisteredHooks &&
                   TryGetReloadedAttachmentDepth(weaponItem, attachmentItem, out depth);
        }

        private static bool TryGetAttachmentDepthLocal(Item weaponItem, Item attachmentItem, out float depth)
        {
            depth = 0.0f;
            if (!TryGetValidState(weaponItem, out GunsmithSpriteState state) || state.Layers.Count == 0)
            {
                return false;
            }

            if (!TryGetAttachmentLayer(state, attachmentItem, out GunsmithLayer layer))
            {
                return false;
            }

            float baseDepth = weaponItem.activeSprite?.Depth ?? weaponItem.Sprite?.Depth ?? attachmentItem.Sprite?.Depth ?? 0.0f;
            int minOrder = state.Layers[0].Order;
            int maxOrder = state.Layers[^1].Order;
            if (minOrder == maxOrder)
            {
                depth = MathHelper.Clamp(baseDepth, 0.0f, 0.999f);
                return true;
            }

            float normalizedOrder = MathHelper.Clamp((layer.Order - minOrder) / (float)(maxOrder - minOrder), 0.0f, 1.0f);
            const float maxDepthOffset = 0.0025f;
            float depthOffset = MathHelper.Lerp(maxDepthOffset, -maxDepthOffset, normalizedOrder);
            depth = MathHelper.Clamp(baseDepth + depthOffset, 0.0f, 0.999f);
            return true;
        }

        internal static object? ResolveReloadDepthBridge(object?[] args)
        {
            if (args.Length != 3 ||
                args[0] is not Item weaponItem ||
                args[1] is not Item attachmentItem ||
                !TryGetAttachmentDepth(weaponItem, attachmentItem, out float depth))
            {
                return false;
            }

            args[2] = depth;
            return true;
        }

        private static bool TryGetReloadedAttachmentDepth(Item weaponItem, Item attachmentItem, out float depth)
        {
            depth = 0.0f;
            object[] payload = { weaponItem, attachmentItem, 0.0f };
            if (GunsmithLuaHooks.Call(ReloadDepthBridgeHookName, payload) is not true ||
                payload[2] is not float resolvedDepth ||
                !float.IsFinite(resolvedDepth))
            {
                return false;
            }

            depth = resolvedDepth;
            return true;
        }

        private static bool TryGetAttachmentLayer(GunsmithSpriteState state, Item attachmentItem, out GunsmithLayer layer)
        {
            string itemIdentifier = attachmentItem.Prefab.Identifier.Value;
            if (!string.IsNullOrWhiteSpace(itemIdentifier))
            {
                foreach (GunsmithLayer candidate in state.Layers)
                {
                    if (string.Equals(candidate.ItemIdentifier, itemIdentifier, StringComparison.OrdinalIgnoreCase))
                    {
                        layer = candidate;
                        return true;
                    }
                }

                foreach (GunsmithLayer candidate in state.Layers)
                {
                    if (string.Equals(candidate.PartId, itemIdentifier, StringComparison.OrdinalIgnoreCase))
                    {
                        layer = candidate;
                        return true;
                    }
                }
            }

            layer = null!;
            return false;
        }

        internal static void RemoveState(Item item)
        {
            RemoveRuntimeState(item);
            GunsmithQuickSlotLayoutPatch.ClearLayouts(item);
            GunsmithQuickAttachmentBarrelTransforms.ClearTransforms(item);
            GunsmithQuickAttachmentTransformService.ClearItemState(item);
            GunsmithHiddenQuickSlotsPatch.ClearItemState(item);
            GunsmithQuickSlotLightPatch.ClearItemState(item);
            if (spriteStates.TryRemove(item, out GunsmithSpriteState? state))
            {
                if (!state.Texture.IsDisposed)
                {
                    state.Texture.Dispose();
                }
                if (!state.InventoryTexture.IsDisposed)
                {
                    state.InventoryTexture.Dispose();
                }
                if (!state.WorldTexture.IsDisposed)
                {
                    state.WorldTexture.Dispose();
                }
            }
        }

        internal static void ApplyState(Item item, GunsmithSpriteState state, bool forceWorldSprite = false)
        {
            if (!IsStateUsable(state))
            {
                ClearFromLua(item);
                return;
            }

            item.OverrideInventorySprite = state.InventorySprite;
            if (HasRegisteredWeaponTag(item) || forceWorldSprite || ReferenceEquals(item.activeSprite, item.Prefab.Sprite))
            {
                item.activeSprite = state.WorldSprite;
            }
        }

        internal static void EnsureSafeSprite(Item item)
        {
            if (spriteStates.TryGetValue(item, out GunsmithSpriteState? state) && !IsStateUsable(state))
            {
                ClearFromLua(item);
            }
        }

        private static bool IsStateUsable(GunsmithSpriteState state)
            => state.Texture != null && !state.Texture.IsDisposed &&
               state.WorldTexture != null && !state.WorldTexture.IsDisposed &&
               state.InventoryTexture != null && !state.InventoryTexture.IsDisposed;

        private static void RestoreVanillaSprite(Item item)
        {
            item.OverrideInventorySprite = null;
            item.activeSprite = item.Prefab.Sprite;
            item.SetActiveSprite();
        }

        public static void Dispose()
        {
            SpriteBatch? disposingSpriteBatch = spriteBatch;
            spriteBatch = null;
            graphicsDevice = null;

            GunsmithGui.Reset();
            GunsmithFabricatorClientPatch.Reset();

            foreach (KeyValuePair<Item, GunsmithSpriteState> pair in spriteStates.ToArray())
            {
                try
                {
                    if (!pair.Key.Removed)
                    {
                        RestoreVanillaSprite(pair.Key);
                    }
                }
                catch (Exception ex)
                {
                    LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to restore a vanilla sprite during cleanup: {ex.Message}");
                }

                try
                {
                    RemoveState(pair.Key);
                }
                catch (Exception ex)
                {
                    LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to dispose a generated sprite during cleanup: {ex.Message}");
                }
            }

            GunsmithQuickSlotLightPatch.ClearAllState();
            GunsmithHiddenQuickSlotsPatch.Reset();
            GunsmithQuickSlotLayoutPatch.ClearAllLayouts();
            GunsmithQuickAttachmentTransformService.ClearAllState();
            WeaponTags.Clear();

            foreach (Texture2D texture in textureCache.Values)
            {
                if (!texture.IsDisposed)
                {
                    texture.Dispose();
                }
            }
            textureCache.Clear();

            disposingSpriteBatch?.Dispose();
        }
    }
}
