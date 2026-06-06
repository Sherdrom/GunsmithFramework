namespace GunsmithFramework
{
    public readonly record struct GunsmithQuickAttachmentTransform(
        Item WeaponItem,
        Item AttachmentItem,
        int QuickSlotIndex,
        Vector2 WorldPosition,
        Vector2 DrawPosition,
        float WorldRotation,
        float DrawRotation,
        Vector2 Direction,
        float FacingDirection,
        Submarine? Submarine,
        Hull? CurrentHull);

    public static class GunsmithQuickAttachmentTransformService
    {
        private static readonly ConditionalWeakTable<Item, QuickSlotIndexCacheBox> QuickSlotIndexCacheByAttachment = new();
        private static readonly ConditionalWeakTable<Item, Dictionary<int, ItemLocalPositionCache>> ItemLocalPositionCacheByWeapon = new();

        internal static void ClearItemState(Item item)
        {
            if (item == null) { return; }

            QuickSlotIndexCacheByAttachment.Remove(item);
            ItemLocalPositionCacheByWeapon.Remove(item);
            if (item.OwnInventory?.slots == null)
            {
                return;
            }

            for (int i = 0; i < item.OwnInventory.slots.Length; i++)
            {
                foreach (Item contained in item.OwnInventory.slots[i].Items)
                {
                    if (contained != null)
                    {
                        QuickSlotIndexCacheByAttachment.Remove(contained);
                    }
                }
            }
        }

        public static bool TryGetTransform(Item attachmentItem, out GunsmithQuickAttachmentTransform transform)
        {
            transform = default;
            if (attachmentItem == null || attachmentItem.Removed)
            {
                return false;
            }

            if (attachmentItem.ParentInventory?.Owner is not Item weaponItem)
            {
                return false;
            }

            return TryGetTransform(weaponItem, attachmentItem, out transform);
        }

        public static bool TryGetTransform(Item weaponItem, Item attachmentItem, out GunsmithQuickAttachmentTransform transform)
        {
            transform = default;
            if (!TryGetDirectQuickSlotIndex(weaponItem, attachmentItem, out int quickSlotIndex))
            {
                return false;
            }

            return TryGetTransformForSlot(weaponItem, attachmentItem, quickSlotIndex, out transform);
        }

        public static bool TryGetTransform(Item weaponItem, Item attachmentItem, int quickSlotIndex, out GunsmithQuickAttachmentTransform transform)
        {
            transform = default;
            if (!IsValidQuickSlotAttachment(weaponItem, attachmentItem, quickSlotIndex))
            {
                return false;
            }

            return TryGetTransformForSlot(weaponItem, attachmentItem, quickSlotIndex, out transform);
        }

        private static bool TryGetTransformForSlot(Item weaponItem, Item attachmentItem, int quickSlotIndex, out GunsmithQuickAttachmentTransform transform)
        {
            transform = default;
            if (!GunsmithQuickSlotLayoutPatch.TryGetLayoutRule(weaponItem, quickSlotIndex, out GunsmithQuickSlotLayoutPatch.QuickSlotLayoutRule rule))
            {
                return false;
            }

            return TryCreateTransform(weaponItem, attachmentItem, quickSlotIndex, rule, out transform);
        }

        internal static bool TryCreateTransform(
            Item weaponItem,
            Item attachmentItem,
            int quickSlotIndex,
            GunsmithQuickSlotLayoutPatch.QuickSlotLayoutRule rule,
            out GunsmithQuickAttachmentTransform transform)
        {
            transform = default;
            if (weaponItem == null || weaponItem.Removed || attachmentItem == null || attachmentItem.Removed)
            {
                return false;
            }

            if (!TryGetItemLocalPosition(weaponItem, quickSlotIndex, rule, out Vector2 itemLocalPos))
            {
                return false;
            }

            bool hasWorldPosition = GunsmithQuickTransformMath.TryItemLocalToWorldPosition(
                weaponItem,
                itemLocalPos,
                drawPosition: false,
                out Vector2 worldPosition);
            bool hasDrawPosition = GunsmithQuickTransformMath.TryItemLocalToWorldPosition(
                weaponItem,
                itemLocalPos,
                drawPosition: true,
                out Vector2 drawPosition);
            float worldRotation = ToWorldRotation(weaponItem, rule.RotationDegrees, drawPosition: false);
            float drawRotation = ToWorldRotation(weaponItem, rule.RotationDegrees, drawPosition: true);
            Vector2 direction = ToForwardDirection(weaponItem, rule.RotationDegrees, drawPosition: true);
            if (!hasWorldPosition ||
                !hasDrawPosition ||
                !float.IsFinite(worldRotation) ||
                !float.IsFinite(drawRotation) ||
                !GunsmithQuickTransformMath.IsFinite(direction) ||
                direction.LengthSquared() < 0.0001f)
            {
                DebugConsole.ThrowError(
                    $"GunsmithFramework QAT produced an invalid quick attachment transform. " +
                    $"weapon={weaponItem.Prefab.Identifier.Value}, " +
                    $"attachment={attachmentItem.Prefab.Identifier.Value}, " +
                    $"slot={quickSlotIndex}, " +
                    $"worldPosition={worldPosition}, drawPosition={drawPosition}, " +
                    $"worldRotation={worldRotation}, drawRotation={drawRotation}, direction={direction}");
                return false;
            }
            direction.Normalize();

            transform = new GunsmithQuickAttachmentTransform(
                weaponItem,
                attachmentItem,
                quickSlotIndex,
                worldPosition,
                drawPosition,
                worldRotation,
                drawRotation,
                direction,
                GetFacingDirection(weaponItem),
                weaponItem.Submarine,
                weaponItem.CurrentHull);
            return true;
        }

        private static bool TryGetDirectQuickSlotIndex(Item weaponItem, Item attachmentItem, out int quickSlotIndex)
        {
            quickSlotIndex = -1;
            if (weaponItem == null || weaponItem.Removed || attachmentItem == null || attachmentItem.Removed || weaponItem.OwnInventory?.slots == null)
            {
                return false;
            }

            Inventory inventory = weaponItem.OwnInventory;
            if (TryGetCachedQuickSlotIndex(attachmentItem, inventory, out quickSlotIndex))
            {
                return true;
            }

            for (int i = 0; i < inventory.slots.Length; i++)
            {
                if (inventory.slots[i].Items.Contains(attachmentItem))
                {
                    quickSlotIndex = i;
                    SetCachedQuickSlotIndex(attachmentItem, inventory, quickSlotIndex);
                    return true;
                }
            }

            QuickSlotIndexCacheByAttachment.Remove(attachmentItem);
            return false;
        }

        private static bool IsValidQuickSlotAttachment(Item weaponItem, Item attachmentItem, int quickSlotIndex)
        {
            if (weaponItem == null || weaponItem.Removed || attachmentItem == null || attachmentItem.Removed || weaponItem.OwnInventory?.slots == null)
            {
                return false;
            }

            return IsCachedSlotStillValid(weaponItem.OwnInventory, attachmentItem, quickSlotIndex);
        }

        private static bool TryGetItemLocalPosition(
            Item weaponItem,
            int quickSlotIndex,
            GunsmithQuickSlotLayoutPatch.QuickSlotLayoutRule rule,
            out Vector2 itemLocalPosition)
        {
            itemLocalPosition = Vector2.Zero;
            if (!GunsmithApi.TryGetSpriteSignature(weaponItem, out string spriteSignature))
            {
                return false;
            }

            if (TryGetCachedItemLocalPosition(weaponItem, quickSlotIndex, rule, spriteSignature, out itemLocalPosition))
            {
                return true;
            }

            if (!GunsmithApi.TryCanvasPointToItemLocal(weaponItem, rule.CanvasAnchor, out itemLocalPosition))
            {
                return false;
            }

            itemLocalPosition += rule.ItemPosOffset;
            SetCachedItemLocalPosition(weaponItem, quickSlotIndex, rule, spriteSignature, itemLocalPosition);
            return true;
        }

        private static bool TryGetCachedItemLocalPosition(
            Item weaponItem,
            int quickSlotIndex,
            GunsmithQuickSlotLayoutPatch.QuickSlotLayoutRule rule,
            string spriteSignature,
            out Vector2 itemLocalPosition)
        {
            itemLocalPosition = Vector2.Zero;
            if (!ItemLocalPositionCacheByWeapon.TryGetValue(weaponItem, out Dictionary<int, ItemLocalPositionCache>? cacheBySlot))
            {
                return false;
            }

            lock (cacheBySlot)
            {
                if (!cacheBySlot.TryGetValue(quickSlotIndex, out ItemLocalPositionCache cache) ||
                    cache.SpriteSignature != spriteSignature ||
                    cache.CanvasAnchor != rule.CanvasAnchor ||
                    cache.ItemPosOffset != rule.ItemPosOffset)
                {
                    return false;
                }

                itemLocalPosition = cache.ItemLocalPosition;
                return true;
            }
        }

        private static void SetCachedItemLocalPosition(
            Item weaponItem,
            int quickSlotIndex,
            GunsmithQuickSlotLayoutPatch.QuickSlotLayoutRule rule,
            string spriteSignature,
            Vector2 itemLocalPosition)
        {
            Dictionary<int, ItemLocalPositionCache> cacheBySlot = ItemLocalPositionCacheByWeapon.GetValue(weaponItem, _ => new Dictionary<int, ItemLocalPositionCache>());
            lock (cacheBySlot)
            {
                cacheBySlot[quickSlotIndex] = new ItemLocalPositionCache(spriteSignature, rule.CanvasAnchor, rule.ItemPosOffset, itemLocalPosition);
            }
        }

        private static bool TryGetCachedQuickSlotIndex(Item attachmentItem, Inventory inventory, out int quickSlotIndex)
        {
            quickSlotIndex = -1;
            if (QuickSlotIndexCacheByAttachment.TryGetValue(attachmentItem, out QuickSlotIndexCacheBox? cache) &&
                ReferenceEquals(cache.Inventory, inventory) &&
                IsCachedSlotStillValid(inventory, attachmentItem, cache.QuickSlotIndex))
            {
                quickSlotIndex = cache.QuickSlotIndex;
                return true;
            }

            QuickSlotIndexCacheByAttachment.Remove(attachmentItem);
            return false;
        }

        private static void SetCachedQuickSlotIndex(Item attachmentItem, Inventory inventory, int quickSlotIndex)
        {
            if (QuickSlotIndexCacheByAttachment.TryGetValue(attachmentItem, out QuickSlotIndexCacheBox? cache))
            {
                cache.Inventory = inventory;
                cache.QuickSlotIndex = quickSlotIndex;
                return;
            }

            QuickSlotIndexCacheByAttachment.Add(attachmentItem, new QuickSlotIndexCacheBox(inventory, quickSlotIndex));
        }

        private static bool IsCachedSlotStillValid(Inventory inventory, Item attachmentItem, int quickSlotIndex)
        {
            return inventory?.slots != null &&
                   quickSlotIndex >= 0 &&
                   quickSlotIndex < inventory.slots.Length &&
                   inventory.slots[quickSlotIndex].Items.Contains(attachmentItem);
        }

        private static float ToWorldRotation(Item owner, float localRotationDegrees, bool drawPosition)
        {
            float rotation = MathHelper.ToRadians(localRotationDegrees);
            PhysicsBody? rootBody = owner.RootContainer?.body ?? owner.body;
            if (owner.body != null)
            {
                rotation *= rootBody?.Dir ?? owner.body.Dir;
                rotation += drawPosition ? owner.body.DrawRotation : owner.body.Rotation;
            }
            else
            {
                rotation += -owner.RotationRad;
            }

            return rotation;
        }

        private static Vector2 ToForwardDirection(Item owner, float localRotationDegrees, bool drawPosition)
        {
            float localRotation = MathHelper.ToRadians(localRotationDegrees);
            float rotation;
            PhysicsBody? rootBody = owner.RootContainer?.body ?? owner.body;
            if (owner.body != null)
            {
                float dir = rootBody?.Dir ?? owner.body.Dir;
                rotation = drawPosition ? owner.body.DrawRotation : owner.body.Rotation;
                rotation += dir >= 0.0f ? localRotation : -localRotation - MathHelper.Pi;
            }
            else
            {
                rotation = -owner.RotationRad + localRotation;
                if (owner.FlippedX && owner.Prefab.CanSpriteFlipX)
                {
                    rotation -= MathHelper.Pi;
                }
            }

            return new Vector2(MathF.Cos(rotation), MathF.Sin(rotation));
        }

        private static float GetFacingDirection(Item owner)
        {
            PhysicsBody? rootBody = owner.RootContainer?.body ?? owner.body;
            if (rootBody != null)
            {
                return rootBody.Dir >= 0.0f ? 1.0f : -1.0f;
            }

            return owner.FlippedX && owner.Prefab.CanSpriteFlipX ? -1.0f : 1.0f;
        }

        private sealed class QuickSlotIndexCacheBox
        {
            public QuickSlotIndexCacheBox(Inventory inventory, int quickSlotIndex)
            {
                Inventory = inventory;
                QuickSlotIndex = quickSlotIndex;
            }

            public Inventory Inventory { get; set; }
            public int QuickSlotIndex { get; set; }
        }

        private readonly record struct ItemLocalPositionCache(
            string SpriteSignature,
            Vector2 CanvasAnchor,
            Vector2 ItemPosOffset,
            Vector2 ItemLocalPosition);
    }
}
