namespace GunsmithFramework
{
    [HarmonyPatch]
    public static class GunsmithHiddenQuickSlotsPatch
    {
        private static readonly Dictionary<string, HashSet<int>> ManagedSlotsByItemIdentifier = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Dictionary<int, HashSet<string>>> VisibleWhenContainedByItemIdentifier = new(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Inventory, LayoutCache> OriginalLayoutsByInventory = new();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Inventory, FrameState> FrameStateByInventory = new();
        private static readonly HashSet<Item> QuickMutationItems = new();

        public static void RegisterHiddenSlots(string itemIdentifier, string slotSpec)
        {
            if (string.IsNullOrWhiteSpace(itemIdentifier))
            {
                return;
            }

            HashSet<int> slots = new();
            foreach (string rawSlot in slotSpec.Split(','))
            {
                if (int.TryParse(rawSlot.Trim(), out int slotIndex) && slotIndex >= 0)
                {
                    slots.Add(slotIndex);
                }
            }

            if (slots.Count == 0)
            {
                ManagedSlotsByItemIdentifier.Remove(itemIdentifier);
                return;
            }

            ManagedSlotsByItemIdentifier[itemIdentifier] = slots;
        }

        public static void RegisterVisibleWhenContained(string itemIdentifier, int slotIndex, string identifierSpec)
        {
            if (string.IsNullOrWhiteSpace(itemIdentifier) || slotIndex < 0)
            {
                return;
            }

            HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase);
            foreach (string rawIdentifier in identifierSpec.Split(','))
            {
                string identifier = rawIdentifier.Trim();
                if (!string.IsNullOrWhiteSpace(identifier))
                {
                    identifiers.Add(identifier);
                }
            }

            if (!VisibleWhenContainedByItemIdentifier.TryGetValue(itemIdentifier, out Dictionary<int, HashSet<string>>? rules))
            {
                rules = new Dictionary<int, HashSet<string>>();
                VisibleWhenContainedByItemIdentifier[itemIdentifier] = rules;
            }

            if (identifiers.Count == 0)
            {
                rules.Remove(slotIndex);
                if (rules.Count == 0)
                {
                    VisibleWhenContainedByItemIdentifier.Remove(itemIdentifier);
                }
                return;
            }

            rules[slotIndex] = identifiers;
        }

        public static void BeginQuickSlotMutation(Item item)
        {
            if (item == null || item.Removed) { return; }
            QuickMutationItems.Add(item);
        }

        public static void EndQuickSlotMutation(Item item)
        {
            if (item == null) { return; }
            QuickMutationItems.Remove(item);
        }

        public static bool IsQuickSlotMutation(Item item)
            => item != null && QuickMutationItems.Contains(item);

        public static void ClearItemState(Item item)
        {
            if (item == null) { return; }
            QuickMutationItems.Remove(item);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HideSlot))]
        [HarmonyPrefix]
        private static bool HideManagedQuickSlots(Inventory __instance, int __0, ref bool __result)
        {
            if (!__instance.isSubInventory)
            {
                double now = Timing.TotalTime;
                if (!FrameStateByInventory.TryGetValue(__instance, out FrameState? frameState))
                {
                    frameState = new FrameState();
                    FrameStateByInventory.Add(__instance, frameState);
                }
                if (Math.Abs(frameState.LastPackTime - now) > 0.0001)
                {
                    frameState.LastPackTime = now;
                    PackVisibleSlotsFirst(__instance);
                }
            }

            if (IsManagedSlot(__instance, __0) && !ShouldShowManagedSlot(__instance, __0))
            {
                __result = true;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Update))]
        [HarmonyPrefix]
        private static void PackSubInventoryBeforeSlotUpdate(Inventory __instance, bool subInventory)
        {
            if (!CanPackManagedSubInventory(__instance, subInventory))
            {
                return;
            }

            if (PackVisibleSlotsFirst(__instance, deferRecaptureOnOwnerPlacementChange: true))
            {
                Inventory.RefreshMouseOnInventory();
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Update))]
        [HarmonyPostfix]
        private static void PackSubInventoryAfterSlotUpdate(Inventory __instance, bool subInventory)
        {
            if (!CanPackManagedSubInventory(__instance, subInventory))
            {
                return;
            }

            if (PackVisibleSlotsFirst(__instance))
            {
                Inventory.RefreshMouseOnInventory();
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.UpdateDragging))]
        [HarmonyPrefix]
        private static bool HandleQuickOverlayDraggingBeforeWorldDrop()
        {
            if (GunsmithGui.TryHandleQuickOverlayDragging())
                return false;
            if (GunsmithGui.IsMouseOnQuickBufferInventory && Inventory.DraggingItems.Any())
                return false;
            return true;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.UpdateDragging))]
        [HarmonyPostfix]
        private static void RestoreQuickOverlayDragAfterNativeDragging()
            => GunsmithGui.ReconcilePendingQuickDragAfterNativeDragging();

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RefreshMouseOnInventory))]
        [HarmonyPostfix]
        private static void IncludeGunsmithQuickBufferInventory()
        {
            if (!GunsmithGui.IsMouseOnQuickBufferInventory)
            {
                return;
            }

            HarmonyLib.AccessTools.PropertySetter(typeof(Inventory), nameof(Inventory.IsMouseOnInventory))?
                .Invoke(null, new object[] { true });
        }

        [HarmonyPatch(typeof(Character), nameof(Character.ControlLocalPlayer))]
        [HarmonyPrefix]
        private static bool BlockGunsmithWindowCharacterInput(Character __instance, ref bool moveCam)
        {
            if (__instance == Character.Controlled && GunsmithGui.IsGunsmithWindowBlockingInput && GunsmithGui.ActiveWindowForInputBlock != null)
            {
                moveCam = false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Character), nameof(Character.ControlLocalPlayer))]
        [HarmonyPostfix]
        private static void BlockGunsmithWindowMouseInput(Character __instance)
        {
            if (__instance != Character.Controlled || !GunsmithGui.IsGunsmithWindowBlockingInput || GunsmithGui.ActiveWindowForInputBlock == null)
            {
                return;
            }

            __instance.ClearInput(InputType.Aim);
            __instance.ClearInput(InputType.Shoot);
            __instance.ClearInput(InputType.Use);
            __instance.ClearInput(InputType.Select);
        }

        [HarmonyPatch(typeof(Character), nameof(Character.UpdateLocalCursor))]
        [HarmonyPrefix]
        private static bool KeepGunsmithWindowCursorLocal(Character __instance)
        {
            if (__instance != Character.Controlled || !GunsmithGui.IsGunsmithWindowBlockingInput || GunsmithGui.ActiveWindowForInputBlock == null)
            {
                return true;
            }

            __instance.CursorPosition = __instance.Position + PlayerInput.MouseSpeed.ClampLength(10.0f);
            __instance.SmoothedCursorPosition = __instance.CursorPosition;
            return false;
        }

        [HarmonyPatch(typeof(Character), nameof(Character.DoInteractionUpdate))]
        [HarmonyPrefix]
        private static bool SkipGunsmithWindowBackgroundInteractions(Character __instance)
        {
            if (__instance != Character.Controlled || !GunsmithGui.IsGunsmithWindowBlockingInput || GunsmithGui.ActiveWindowForInputBlock == null)
            {
                return true;
            }

            __instance.FocusedItem = null;
            __instance.FocusedCharacter = null;
            return false;
        }

        [HarmonyPatch(typeof(ItemInventory), nameof(ItemInventory.FindAllowedSlot))]
        [HarmonyPrefix]
        private static bool SkipManagedSlotsWhenAutoPutting(ItemInventory __instance, Item item, bool ignoreCondition, ref int __result)
        {
            if (!HasManagedSlots(__instance) || IsQuickMutationAllowed(__instance))
            {
                return true;
            }

            __result = FindAllowedNonManagedSlot(__instance, item, ignoreCondition);
            return false;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.TryPutItem), typeof(Item), typeof(int), typeof(bool), typeof(bool), typeof(Character), typeof(bool), typeof(bool), typeof(bool))]
        [HarmonyPrefix]
        private static bool SkipSwapForUncontainableItem(
            Inventory __instance,
            Item item,
            int i,
            bool allowSwapping,
            bool allowCombine,
            Character user,
            bool createNetworkEvent,
            bool ignoreCondition,
            bool triggerOnInsertedEffects,
            ref bool __result)
        {
            if (GunsmithGui.TryHandlePendingQuickDragNativeSlotDrop(
                    __instance,
                    item,
                    i,
                    allowSwapping,
                    allowCombine,
                    user,
                    createNetworkEvent,
                    ignoreCondition,
                    triggerOnInsertedEffects,
                    ref __result))
            {
                return false;
            }

            if (!allowSwapping || item == null || item.ParentInventory == null) { return true; }
            if (i < 0 || i >= __instance.slots.Length || !__instance.slots[i].Any()) { return true; }
            if (item.ParentInventory == __instance) { return true; }
            if (__instance is not ItemInventory itemInventory) { return true; }
            if (!itemInventory.Container.CanBeContained(item, i))
            {
                __result = false;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(ItemInventory), nameof(ItemInventory.TryPutItem), typeof(Item), typeof(int), typeof(bool), typeof(bool), typeof(Character), typeof(bool), typeof(bool), typeof(bool))]
        [HarmonyPrefix]
        private static bool BlockDirectManagedSlotPut(ItemInventory __instance, Item item, int i, Character user, bool createNetworkEvent, bool ignoreCondition, bool triggerOnInsertedEffects, ref bool __result)
        {
            if (!IsManagedSlot(__instance, i) || IsQuickMutationAllowed(__instance))
            {
                return true;
            }

            if (IsQuickUiOpenForInventory(__instance))
            {
                __result = false;
                return false;
            }

            Item? containedItem = i >= 0 && i < __instance.slots.Length ? __instance.slots[i].FirstOrDefault() : null;
            ItemInventory? containedInventory = containedItem?.OwnInventory;
            if (containedInventory != null && containedInventory.CanBePut(item))
            {
                __result = containedInventory.TryPutItem(item, user, null, createNetworkEvent, ignoreCondition, triggerOnInsertedEffects);
                return false;
            }

            __result = false;
            return false;
        }

        private static bool CanPackManagedSubInventory(Inventory inventory, bool subInventory)
        {
            return subInventory &&
                   inventory.visualSlots != null &&
                   inventory.visualSlots.Length > 0 &&
                   HasManagedSlots(inventory);
        }

        private static void CaptureOriginalLayouts(Inventory inventory, OwnerPlacement ownerPlacement)
        {
            if (inventory.visualSlots == null || inventory.visualSlots.Length == 0)
            {
                return;
            }

            OriginalLayoutsByInventory.Remove(inventory);
            OriginalLayoutsByInventory.Add(inventory, new LayoutCache(inventory.visualSlots, inventory.visualSlots.Select(SlotLayout.FromVisualSlot).ToArray(), ownerPlacement));
        }

        private static bool PackVisibleSlotsFirst(Inventory __instance, bool deferRecaptureOnOwnerPlacementChange = false)
        {
            VisualSlot[]? visualSlots = __instance.visualSlots;
            if (visualSlots == null || visualSlots.Length == 0)
            {
                return false;
            }

            HashSet<int>? hiddenSlots = GetManagedHiddenSlots(__instance);
            bool hasInjectedSlots = HasInjectedQuickSlots(__instance);
            if ((hiddenSlots == null || hiddenSlots.Count == 0) && !hasInjectedSlots)
            {
                return false;
            }

            OwnerPlacement ownerPlacement = GetOwnerPlacement(__instance);
            if (!TryGetReusableLayoutCache(__instance, visualSlots, ownerPlacement, deferRecaptureOnOwnerPlacementChange, out LayoutCache layoutCache))
            {
                return false;
            }

            bool[] hiddenStates = layoutCache.HiddenStates;
            int visibleCount = 0;
            int hiddenCount = 0;
            int layoutHash = 17;
            for (int i = 0; i < visualSlots.Length; i++)
            {
                bool shouldHide = (hiddenSlots?.Contains(i) == true || IsInjectedQuickSlot(__instance, i)) && !ShouldShowManagedSlot(__instance, i);
                hiddenStates[i] = shouldHide;
                layoutHash = (layoutHash * 31) + (shouldHide ? 1 : 0);
                if (!shouldHide)
                {
                    visibleCount++;
                }
                else
                {
                    hiddenCount++;
                }
            }

            if (hiddenCount == 0)
            {
                if (layoutCache.LastPackHash == layoutHash && IsOriginalLayoutCurrent(layoutCache))
                {
                    return false;
                }

                for (int i = 0; i < visualSlots.Length; i++)
                {
                    layoutCache.Layouts[i].ApplyTo(visualSlots[i]);
                }
                layoutCache.LastPackHash = layoutHash;
                return true;
            }

            if (visibleCount == 0)
            {
                return false;
            }

            if (layoutCache.LastPackHash == layoutHash &&
                IsPackedLayoutCurrent(layoutCache, hiddenStates))
            {
                return false;
            }

            SlotLayout[] originalLayouts = layoutCache.Layouts;
            int displayIndex = 0;
            for (int i = 0; i < visualSlots.Length; i++)
            {
                if (!hiddenStates[i])
                {
                    originalLayouts[displayIndex].ApplyTo(visualSlots[i]);
                    displayIndex++;
                }
            }

            SlotLayout hiddenLayout = originalLayouts[0];
            for (int i = 0; i < visualSlots.Length; i++)
            {
                if (hiddenStates[i])
                {
                    hiddenLayout.ApplyTo(visualSlots[i]);
                }
            }

            layoutCache.LastPackHash = layoutHash;
            return true;
        }

        private static bool IsOriginalLayoutCurrent(LayoutCache layoutCache)
        {
            for (int i = 0; i < layoutCache.VisualSlots.Length; i++)
            {
                if (!layoutCache.Layouts[i].Matches(layoutCache.VisualSlots[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetReusableLayoutCache(
            Inventory inventory,
            VisualSlot[] visualSlots,
            OwnerPlacement ownerPlacement,
            bool deferRecaptureOnOwnerPlacementChange,
            out LayoutCache layoutCache)
        {
            if (OriginalLayoutsByInventory.TryGetValue(inventory, out LayoutCache? existingLayoutCache))
            {
                layoutCache = existingLayoutCache;
                bool ownerPlacementChanged = layoutCache.OwnerPlacement != ownerPlacement;
                if (ownerPlacementChanged && deferRecaptureOnOwnerPlacementChange)
                {
                    OriginalLayoutsByInventory.Remove(inventory);
                    layoutCache = null!;
                    return false;
                }

                if (!ownerPlacementChanged &&
                    layoutCache.VisualSlots == visualSlots &&
                    layoutCache.Layouts.Length == visualSlots.Length &&
                    layoutCache.HiddenStates.Length == visualSlots.Length)
                {
                    return true;
                }
            }

            CaptureOriginalLayouts(inventory, ownerPlacement);
            if (!OriginalLayoutsByInventory.TryGetValue(inventory, out existingLayoutCache))
            {
                layoutCache = null!;
                return false;
            }

            layoutCache = existingLayoutCache;
            return
                   layoutCache.VisualSlots == visualSlots &&
                   layoutCache.Layouts.Length == visualSlots.Length &&
                   layoutCache.HiddenStates.Length == visualSlots.Length;
        }

        private static bool IsPackedLayoutCurrent(LayoutCache layoutCache, bool[] hiddenStates)
        {
            VisualSlot[] visualSlots = layoutCache.VisualSlots;
            SlotLayout[] originalLayouts = layoutCache.Layouts;
            SlotLayout hiddenLayout = originalLayouts[0];
            int displayIndex = 0;
            for (int i = 0; i < visualSlots.Length; i++)
            {
                if (hiddenStates[i])
                {
                    if (!hiddenLayout.Matches(visualSlots[i]))
                    {
                        return false;
                    }
                    continue;
                }

                if (displayIndex >= originalLayouts.Length || !originalLayouts[displayIndex].Matches(visualSlots[i]))
                {
                    return false;
                }
                displayIndex++;
            }

            return true;
        }

        private static bool IsManagedSlot(Inventory inventory, int slotIndex)
        {
            return (GetManagedHiddenSlots(inventory) is HashSet<int> hiddenSlots && hiddenSlots.Contains(slotIndex)) ||
                   IsInjectedQuickSlot(inventory, slotIndex);
        }

        private static bool HasManagedSlots(Inventory inventory)
        {
            return (GetManagedHiddenSlots(inventory) is HashSet<int> hiddenSlots && hiddenSlots.Count > 0) ||
                   HasInjectedQuickSlots(inventory);
        }

        private static bool IsQuickMutationAllowed(Inventory inventory)
        {
            return inventory.Owner is Item item && QuickMutationItems.Contains(item);
        }

        private static int FindAllowedNonManagedSlot(ItemInventory inventory, Item item, bool ignoreCondition)
        {
            if (inventory.ItemOwnsSelf(item) || inventory.Contains(item) || !inventory.container.CanBeContained(item))
            {
                return -1;
            }

            for (int i = 0; i < inventory.capacity; i++)
            {
                if (!IsManagedSlot(inventory, i) && inventory.slots[i].Any() && inventory.CanBePutInSlot(item, i, ignoreCondition))
                {
                    return i;
                }
            }

            for (int i = 0; i < inventory.capacity; i++)
            {
                if (!IsManagedSlot(inventory, i) && inventory.CanBePutInSlot(item, i, ignoreCondition))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool ShouldShowManagedSlot(Inventory inventory, int slotIndex)
        {
            if (inventory.Owner is not Item ownerItem || ownerItem.Removed || ownerItem.Prefab == null) { return false; }
            if (GunsmithGui.IsOpenForItem(ownerItem, quickMode: true)) { return false; }

            string ownerIdentifier = ownerItem.Prefab.Identifier.Value;
            if (!VisibleWhenContainedByItemIdentifier.TryGetValue(ownerIdentifier, out Dictionary<int, HashSet<string>>? rules) ||
                !rules.TryGetValue(slotIndex, out HashSet<string>? identifiers) ||
                identifiers.Count == 0 ||
                slotIndex < 0 ||
                slotIndex >= inventory.slots.Length)
            {
                return false;
            }

            foreach (Item contained in inventory.slots[slotIndex].Items)
            {
                if (contained?.Prefab == null || contained.Removed) { continue; }
                string containedIdentifier = contained.Prefab.Identifier.Value;
                if (identifiers.Contains(containedIdentifier))
                {
                    return true;
                }

                foreach (string identifierOrTag in identifiers)
                {
                    if (contained.HasTag(identifierOrTag))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsQuickUiOpenForInventory(Inventory inventory)
            => inventory.Owner is Item ownerItem &&
               !ownerItem.Removed &&
               GunsmithGui.IsOpenForItem(ownerItem, quickMode: true);

        private static HashSet<int>? GetManagedHiddenSlots(Inventory inventory)
        {
            if (inventory.Owner is not Item item || item.Removed || item.Prefab == null) { return null; }
            string itemIdentifier = item.Prefab.Identifier.Value;
            return ManagedSlotsByItemIdentifier.TryGetValue(itemIdentifier, out HashSet<int>? hiddenSlots) ? hiddenSlots : null;
        }

        private static bool IsInjectedQuickSlot(Inventory inventory, int slotIndex)
        {
            if (inventory.Owner is not Item item || item.Removed || item.Prefab == null)
            {
                return false;
            }

            return GunsmithQuickSlotCapacityPatch.IsInjectedQuickSlot(item.Prefab.Identifier.Value, slotIndex);
        }

        private static bool HasInjectedQuickSlots(Inventory inventory)
        {
            if (inventory.Owner is not Item item || item.Removed || item.Prefab == null)
            {
                return false;
            }

            return GunsmithQuickSlotCapacityPatch.HasInjectedQuickSlots(item.Prefab.Identifier.Value);
        }

        private static OwnerPlacement GetOwnerPlacement(Inventory inventory)
        {
            if (inventory.Owner is not Item ownerItem || ownerItem.Removed)
            {
                return default;
            }

            Inventory? parentInventory = ownerItem.ParentInventory;
            int parentSlotIndex = parentInventory?.FindIndex(ownerItem) ?? -1;
            Rectangle parentSlotRect = default;
            VisualSlot[]? parentVisualSlots = parentInventory?.visualSlots;
            if (parentVisualSlots != null &&
                parentSlotIndex >= 0 &&
                parentSlotIndex < parentVisualSlots.Length)
            {
                parentSlotRect = parentVisualSlots[parentSlotIndex].Rect;
            }

            return new OwnerPlacement(ownerItem, parentInventory, parentSlotIndex, parentSlotRect);
        }

        private readonly record struct OwnerPlacement(Item? OwnerItem, Inventory? ParentInventory, int ParentSlotIndex, Rectangle ParentSlotRect);

        private sealed class LayoutCache
        {
            public readonly VisualSlot[] VisualSlots;
            public readonly SlotLayout[] Layouts;
            public readonly bool[] HiddenStates;
            public readonly OwnerPlacement OwnerPlacement;
            public int LastPackHash;

            public LayoutCache(VisualSlot[] visualSlots, SlotLayout[] layouts, OwnerPlacement ownerPlacement)
            {
                VisualSlots = visualSlots;
                Layouts = layouts;
                HiddenStates = new bool[visualSlots.Length];
                OwnerPlacement = ownerPlacement;
                LastPackHash = 0;
            }
        }

        private sealed class FrameState
        {
            public double LastPackTime;
        }

        private readonly struct SlotLayout
        {
            private readonly Rectangle rect;
            private readonly Rectangle interactRect;
            private readonly int subInventoryDir;

            private SlotLayout(Rectangle rect, Rectangle interactRect, int subInventoryDir)
            {
                this.rect = rect;
                this.interactRect = interactRect;
                this.subInventoryDir = subInventoryDir;
            }

            public static SlotLayout FromVisualSlot(VisualSlot slot)
            {
                return new SlotLayout(slot.Rect, slot.InteractRect, slot.SubInventoryDir);
            }

            public void ApplyTo(VisualSlot slot)
            {
                slot.Rect = rect;
                slot.InteractRect = interactRect;
                slot.SubInventoryDir = subInventoryDir;
            }

            public bool Matches(VisualSlot slot)
            {
                return slot.Rect == rect &&
                       slot.InteractRect == interactRect &&
                       slot.SubInventoryDir == subInventoryDir;
            }
        }
    }
}
