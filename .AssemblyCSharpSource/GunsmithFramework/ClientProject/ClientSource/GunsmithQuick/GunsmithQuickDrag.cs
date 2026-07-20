namespace GunsmithFramework
{
    internal static class GunsmithQuickDrag
    {
        private sealed class PendingQuickDrag
        {
            public readonly Item WeaponItem;
            public readonly string SlotPath;
            public readonly int SlotIndex;
            public readonly Item DraggedItem;

            public PendingQuickDrag(Item weaponItem, string slotPath, int slotIndex, Item draggedItem)
            {
                WeaponItem = weaponItem;
                SlotPath = slotPath;
                SlotIndex = slotIndex;
                DraggedItem = draggedItem;
            }
        }

        private static PendingQuickDrag? pendingQuickDrag;
        private static bool handlingNativeQuickDragDrop;
        private static Item? pendingNativeQuickDragDropClearItem;

        internal static bool IsPendingSource(Item weaponItem, string slotPath, int slotIndex)
            => pendingQuickDrag != null &&
               ReferenceEquals(pendingQuickDrag.WeaponItem, weaponItem) &&
               string.Equals(pendingQuickDrag.SlotPath, slotPath, StringComparison.Ordinal) &&
               pendingQuickDrag.SlotIndex == slotIndex;

        internal static bool MatchesPendingSource(Item weaponItem, Item draggedItem, string slotPath, int slotIndex)
            => IsPendingSource(weaponItem, slotPath, slotIndex) &&
               ReferenceEquals(pendingQuickDrag!.DraggedItem, draggedItem);

        internal static bool Begin(Item weaponItem, string slotPath, int slotIndex, Item draggedItem)
        {
            if (weaponItem.OwnInventory == null ||
                slotIndex < 0 ||
                slotIndex >= weaponItem.OwnInventory.slots.Length ||
                draggedItem.Removed ||
                !RemoveItemFromWeaponInventory(weaponItem, draggedItem))
            {
                return false;
            }

            Inventory.DraggingItems.Clear();
            Inventory.DraggingItems.Add(draggedItem);
            Inventory.DraggingSlot = null;
            pendingQuickDrag = new PendingQuickDrag(weaponItem, slotPath, slotIndex, draggedItem);
            return true;
        }

        internal static bool TryPlace(
            Item weaponItem,
            string slotPath,
            int slotIndex,
            IReadOnlySet<string> allowedItemIdentifiers,
            Item draggedItem)
        {
            if (weaponItem.OwnInventory == null || slotIndex < 0 || draggedItem.Removed)
            {
                return false;
            }

            if (MatchesPendingSource(weaponItem, draggedItem, slotPath, slotIndex))
            {
                bool restored = PutItemInWeaponSlot(weaponItem, draggedItem, slotIndex);
                if (restored)
                {
                    pendingQuickDrag = null;
                }
                return CompleteOverlayDrop(restored);
            }

            if (!IsDraggedItemAllowedByQuickSlot(allowedItemIdentifiers, draggedItem))
            {
                return false;
            }

            Item? existingItem = GetContainedQuickItem(weaponItem, slotIndex);
            if (existingItem != null)
            {
                if (ReferenceEquals(existingItem, draggedItem))
                {
                    pendingQuickDrag = null;
                    Sync(weaponItem);
                    return CompleteOverlayDrop(true);
                }

                bool placed = pendingQuickDrag != null && ReferenceEquals(pendingQuickDrag.DraggedItem, draggedItem)
                    ? TrySwapPendingQuickDraggedItemIntoQuickSlot(weaponItem, slotIndex, draggedItem, existingItem)
                    : TryReplaceExternalDraggedItemIntoQuickSlot(weaponItem, slotIndex, draggedItem, existingItem);
                return CompleteOverlayDrop(placed);
            }

            if (!PutItemInWeaponSlot(weaponItem, draggedItem, slotIndex))
            {
                return false;
            }

            if (pendingQuickDrag != null && ReferenceEquals(pendingQuickDrag.DraggedItem, draggedItem))
            {
                pendingQuickDrag = null;
            }
            Sync(weaponItem);
            return CompleteOverlayDrop(true);
        }

        internal static bool TryHandleNativeSlotDrop(
            Inventory targetInventory,
            Item draggedItem,
            int targetSlotIndex,
            bool allowSwapping,
            bool allowCombine,
            Character user,
            bool createNetworkEvent,
            bool ignoreCondition,
            bool triggerOnInsertedEffects,
            ref bool result)
        {
            result = false;
            if (handlingNativeQuickDragDrop ||
                pendingQuickDrag == null ||
                draggedItem == null ||
                draggedItem.Removed ||
                !allowSwapping ||
                !ReferenceEquals(pendingQuickDrag.DraggedItem, draggedItem) ||
                targetInventory == null ||
                targetSlotIndex < 0 ||
                targetSlotIndex >= targetInventory.slots.Length)
            {
                return false;
            }

            PendingQuickDrag drag = pendingQuickDrag;
            if (drag.WeaponItem.Removed ||
                drag.WeaponItem.OwnInventory == null ||
                ReferenceEquals(targetInventory, drag.WeaponItem.OwnInventory))
            {
                return false;
            }

            Item? existingItem = null;
            foreach (Item contained in targetInventory.slots[targetSlotIndex].Items)
            {
                if (contained != null &&
                    !contained.Removed &&
                    !ReferenceEquals(contained, draggedItem))
                {
                    existingItem = contained;
                    break;
                }
            }
            if (existingItem == null || CanNativeInventoryCombine(draggedItem, existingItem, allowCombine))
            {
                return false;
            }

            handlingNativeQuickDragDrop = true;
            try
            {
                if (!PutItemInWeaponSlot(drag.WeaponItem, existingItem, drag.SlotIndex, allowSwapping: false))
                {
                    return false;
                }

                if (!targetInventory.TryPutItem(
                        draggedItem,
                        targetSlotIndex,
                        allowSwapping: false,
                        allowCombine: false,
                        user,
                        createNetworkEvent,
                        ignoreCondition,
                        triggerOnInsertedEffects))
                {
                    RemoveItemFromWeaponInventory(drag.WeaponItem, existingItem);
                    targetInventory.TryPutItem(
                        existingItem,
                        targetSlotIndex,
                        allowSwapping: false,
                        allowCombine: false,
                        user,
                        createNetworkEvent,
                        ignoreCondition: true,
                        triggerOnInsertedEffects: false);
                    result = false;
                    return true;
                }

                pendingQuickDrag = null;
                pendingNativeQuickDragDropClearItem = draggedItem;
                Sync(drag.WeaponItem);
                result = true;
                return true;
            }
            finally
            {
                handlingNativeQuickDragDrop = false;
            }
        }

        internal static bool ReconcileAfterNativeDragging()
        {
            ClearPendingNativeDrop();

            if (pendingQuickDrag == null || handlingNativeQuickDragDrop)
            {
                return false;
            }

            PendingQuickDrag drag = pendingQuickDrag;
            if (drag.WeaponItem.Removed || drag.DraggedItem.Removed)
            {
                pendingQuickDrag = null;
                if (drag.DraggedItem.Removed)
                {
                    ClearDraggingItem(drag.DraggedItem);
                }
                if (!drag.WeaponItem.Removed)
                {
                    Sync(drag.WeaponItem);
                }
                return false;
            }

            bool stillDragging = Inventory.DraggingItems.Contains(drag.DraggedItem);
            if (drag.DraggedItem.ParentInventory != null)
            {
                pendingQuickDrag = null;
                Sync(drag.WeaponItem);
                return false;
            }

            if (stillDragging && PlayerInput.PrimaryMouseButtonHeld())
            {
                return false;
            }

            if (Inventory.IsMouseOnInventory)
            {
                return Restore(syncLua: true);
            }

            pendingQuickDrag = null;
            Sync(drag.WeaponItem);
            return false;
        }

        internal static bool RestoreOrKeepDragging(Item draggedItem, bool syncLua)
        {
            bool restored = Restore(syncLua);
            if (!restored)
            {
                EnsureDragging(draggedItem);
            }
            return restored;
        }

        internal static bool Restore(bool syncLua)
        {
            if (pendingQuickDrag == null)
            {
                return false;
            }

            PendingQuickDrag drag = pendingQuickDrag;
            bool restored = false;
            if (!drag.WeaponItem.Removed && !drag.DraggedItem.Removed)
            {
                restored = PutItemInWeaponSlot(drag.WeaponItem, drag.DraggedItem, drag.SlotIndex) ||
                    TryReturnItemToControlledInventory(drag.DraggedItem);
            }

            if (!restored && !drag.WeaponItem.Removed && !drag.DraggedItem.Removed)
            {
                EnsureDragging(drag.DraggedItem);
                return false;
            }

            pendingQuickDrag = null;
            if (restored || drag.DraggedItem.Removed)
            {
                ClearDraggingItem(drag.DraggedItem);
            }

            if (syncLua && !drag.WeaponItem.Removed)
            {
                Sync(drag.WeaponItem);
            }

            return restored;
        }

        internal static void Reset()
        {
            Restore(syncLua: false);
            ClearPendingNativeDrop();
            pendingQuickDrag = null;
            handlingNativeQuickDragDrop = false;
            pendingNativeQuickDragDropClearItem = null;
        }

        internal static bool CanNativeInventoryCombine(bool allowCombine, string draggedIdentifier, string existingIdentifier)
            => allowCombine &&
               !string.IsNullOrWhiteSpace(draggedIdentifier) &&
               string.Equals(draggedIdentifier, existingIdentifier, StringComparison.OrdinalIgnoreCase);

        private static bool CanNativeInventoryCombine(Item draggedItem, Item existingItem, bool allowCombine)
        {
            if (draggedItem.Removed || existingItem.Removed)
            {
                return false;
            }

            return CanNativeInventoryCombine(
                allowCombine,
                draggedItem.Prefab?.Identifier.Value ?? string.Empty,
                existingItem.Prefab?.Identifier.Value ?? string.Empty);
        }

        private static bool IsDraggedItemAllowedByQuickSlot(IReadOnlySet<string> allowedItemIdentifiers, Item draggedItem)
        {
            string identifier = draggedItem.Prefab?.Identifier.Value ?? string.Empty;
            return !string.IsNullOrWhiteSpace(identifier) && allowedItemIdentifiers.Contains(identifier);
        }

        private static bool PutItemInWeaponSlot(Item weaponItem, Item itemToPut, int slotIndex, bool allowSwapping = true)
        {
            if (weaponItem.OwnInventory == null || slotIndex < 0 || slotIndex >= weaponItem.OwnInventory.slots.Length)
            {
                return false;
            }

            GunsmithHiddenQuickSlotsPatch.BeginQuickSlotMutation(weaponItem);
            try
            {
                return weaponItem.OwnInventory.TryPutItem(itemToPut, slotIndex, allowSwapping, allowCombine: false, Character.Controlled, createNetworkEvent: false, ignoreCondition: false, triggerOnInsertedEffects: false);
            }
            finally
            {
                GunsmithHiddenQuickSlotsPatch.EndQuickSlotMutation(weaponItem);
            }
        }

        private static bool TryReplaceExternalDraggedItemIntoQuickSlot(Item weaponItem, int targetSlotIndex, Item draggedItem, Item existingItem)
        {
            if (weaponItem.OwnInventory == null ||
                targetSlotIndex < 0 ||
                targetSlotIndex >= weaponItem.OwnInventory.slots.Length ||
                existingItem.Removed ||
                draggedItem.Removed)
            {
                return false;
            }

            Inventory? sourceInventory = draggedItem.ParentInventory ?? Inventory.DraggingInventory;
            int sourceSlotIndex = sourceInventory?.FindIndex(draggedItem) ?? -1;
            bool canRestoreToExactSourceSlot = sourceInventory != null &&
                !ReferenceEquals(sourceInventory, weaponItem.OwnInventory) &&
                sourceSlotIndex >= 0 &&
                sourceSlotIndex < sourceInventory.slots.Length;

            if (!RemoveItemFromWeaponInventory(weaponItem, existingItem))
            {
                return false;
            }

            if (!PutItemInWeaponSlot(weaponItem, draggedItem, targetSlotIndex, allowSwapping: false))
            {
                PutItemInWeaponSlot(weaponItem, existingItem, targetSlotIndex, allowSwapping: false);
                return false;
            }

            if (canRestoreToExactSourceSlot &&
                TryPutItemInInventorySlot(sourceInventory!, existingItem, sourceSlotIndex, Character.Controlled, createNetworkEvent: false, ignoreCondition: true, triggerOnInsertedEffects: false))
            {
                Sync(weaponItem);
                return true;
            }

            if (TryReturnItemToControlledInventory(existingItem))
            {
                Sync(weaponItem);
                return true;
            }

            RemoveItemFromWeaponInventory(weaponItem, draggedItem);
            PutItemInWeaponSlot(weaponItem, existingItem, targetSlotIndex, allowSwapping: false);
            if (canRestoreToExactSourceSlot)
            {
                TryPutItemInInventorySlot(sourceInventory!, draggedItem, sourceSlotIndex, Character.Controlled, createNetworkEvent: false, ignoreCondition: true, triggerOnInsertedEffects: false);
            }
            else
            {
                TryReturnItemToControlledInventory(draggedItem);
            }
            return false;
        }

        private static bool TrySwapPendingQuickDraggedItemIntoQuickSlot(Item weaponItem, int targetSlotIndex, Item draggedItem, Item existingItem)
        {
            if (pendingQuickDrag == null ||
                weaponItem.OwnInventory == null ||
                targetSlotIndex < 0 ||
                targetSlotIndex >= weaponItem.OwnInventory.slots.Length ||
                existingItem.Removed ||
                draggedItem.Removed)
            {
                return false;
            }

            PendingQuickDrag drag = pendingQuickDrag;
            if (!ReferenceEquals(drag.WeaponItem, weaponItem) ||
                !ReferenceEquals(drag.DraggedItem, draggedItem) ||
                drag.SlotIndex < 0 ||
                drag.SlotIndex >= weaponItem.OwnInventory.slots.Length ||
                drag.SlotIndex == targetSlotIndex)
            {
                return false;
            }

            if (!RemoveItemFromWeaponInventory(weaponItem, existingItem))
            {
                return false;
            }

            if (!PutItemInWeaponSlot(weaponItem, draggedItem, targetSlotIndex, allowSwapping: false))
            {
                PutItemInWeaponSlot(weaponItem, existingItem, targetSlotIndex, allowSwapping: false);
                return false;
            }

            if (!PutItemInWeaponSlot(weaponItem, existingItem, drag.SlotIndex, allowSwapping: false))
            {
                RemoveItemFromWeaponInventory(weaponItem, draggedItem);
                PutItemInWeaponSlot(weaponItem, existingItem, targetSlotIndex, allowSwapping: false);
                PutItemInWeaponSlot(weaponItem, draggedItem, drag.SlotIndex, allowSwapping: false);
                return false;
            }

            pendingQuickDrag = null;
            Sync(weaponItem);
            return true;
        }

        private static bool TryPutItemInInventorySlot(
            Inventory inventory,
            Item itemToPut,
            int slotIndex,
            Character user,
            bool createNetworkEvent,
            bool ignoreCondition,
            bool triggerOnInsertedEffects)
        {
            if (itemToPut.Removed || slotIndex < 0 || slotIndex >= inventory.slots.Length)
            {
                return false;
            }

            return inventory.TryPutItem(
                itemToPut,
                slotIndex,
                allowSwapping: false,
                allowCombine: false,
                user,
                createNetworkEvent,
                ignoreCondition,
                triggerOnInsertedEffects);
        }

        private static bool TryReturnItemToControlledInventory(Item itemToReturn)
        {
            if (itemToReturn.Removed || Character.Controlled?.Inventory == null)
            {
                return false;
            }

            return Character.Controlled.Inventory.TryPutItem(
                itemToReturn,
                Character.Controlled,
                CharacterInventory.AnySlot,
                createNetworkEvent: false,
                ignoreCondition: true,
                triggerOnInsertedEffects: false);
        }

        private static bool RemoveItemFromWeaponInventory(Item weaponItem, Item itemToRemove)
        {
            if (weaponItem.OwnInventory == null || itemToRemove.Removed || !weaponItem.OwnInventory.Contains(itemToRemove))
            {
                return false;
            }

            GunsmithHiddenQuickSlotsPatch.BeginQuickSlotMutation(weaponItem);
            try
            {
                weaponItem.OwnInventory.RemoveItem(itemToRemove);
                return true;
            }
            finally
            {
                GunsmithHiddenQuickSlotsPatch.EndQuickSlotMutation(weaponItem);
            }
        }

        private static Item? GetContainedQuickItem(Item weaponItem, int slotIndex)
        {
            if (weaponItem.OwnInventory == null || slotIndex < 0 || slotIndex >= weaponItem.OwnInventory.slots.Length)
            {
                return null;
            }

            foreach (Item contained in weaponItem.OwnInventory.slots[slotIndex].Items)
            {
                if (contained != null && !contained.Removed)
                {
                    return contained;
                }
            }
            return null;
        }

        private static bool CompleteOverlayDrop(bool succeeded)
        {
            if (succeeded)
            {
                Inventory.DraggingItems.Clear();
            }
            return succeeded;
        }

        private static void EnsureDragging(Item item)
        {
            if (!item.Removed && !Inventory.DraggingItems.Contains(item))
            {
                Inventory.DraggingItems.Add(item);
            }
        }

        private static void ClearDraggingItem(Item item)
        {
            Inventory.DraggingItems.Remove(item);
            Inventory.DraggingSlot = null;
        }

        private static void ClearPendingNativeDrop()
        {
            if (pendingNativeQuickDragDropClearItem == null)
            {
                return;
            }

            ClearDraggingItem(pendingNativeQuickDragDropClearItem);
            pendingNativeQuickDragDropClearItem = null;
        }

        private static void Sync(Item weaponItem)
            => GunsmithApi.CallLuaHook("GunsmithFrameworkSyncQuickContainer", weaponItem);
    }
}
