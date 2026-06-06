using static GunsmithFramework.GunsmithHookArgs;

namespace GunsmithFramework
{
    public static partial class GunsmithApi
    {
        public static void RegisterLuaHooks(Barotrauma.LuaCs.Compatibility.ILuaCsHook hook)
        {
            GunsmithQuickPartItemSpawner.BeginQuickSlotMutation = GunsmithHiddenQuickSlotsPatch.BeginQuickSlotMutation;
            GunsmithQuickPartItemSpawner.EndQuickSlotMutation = GunsmithHiddenQuickSlotsPatch.EndQuickSlotMutation;

            hook.Add("GunsmithFrameworkApply", args =>
            {
                Item? item = FindArg<Item>(args);
                string? signature = FindStringArg(args, 0);
                string? layerSpec = FindStringArg(args, 1);
                string? inventorySpec = FindStringArg(args, 2);
                string? worldSpec = FindStringArg(args, 3);
                string? statsSpec = FindStringArg(args, 4);
                string? managedItemSpec = FindStringArg(args, 5);
                int width = FindIntArg(args, 0);
                int height = FindIntArg(args, 1);
                if (item != null && signature != null && layerSpec != null)
                {
                    return ApplyFromLua(item, signature, layerSpec, inventorySpec ?? string.Empty, worldSpec ?? string.Empty, statsSpec ?? string.Empty, managedItemSpec ?? string.Empty, width, height);
                }
                return false;
            });

            hook.Add("GunsmithFrameworkClearRuntimeState", args =>
            {
                Item? item = FindArg<Item>(args);
                if (item != null)
                {
                    RemoveState(item);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkOpen", args =>
            {
                Item? item = FindArg<Item>(args);
                string? title = FindStringArg(args, 0);
                string? slotSpec = FindStringArg(args, 1);
                if (item != null && title != null && slotSpec != null)
                {
                    GunsmithGui.OpenFromLua(item, title, slotSpec);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkOpenQuick", args =>
            {
                Item? item = FindArg<Item>(args);
                string? title = FindStringArg(args, 0);
                string? slotSpec = FindStringArg(args, 1);
                if (item != null && title != null && slotSpec != null)
                {
                    GunsmithGui.OpenQuickFromLua(item, title, slotSpec);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkRefreshParts", args =>
            {
                Item? item = FindArg<Item>(args);
                string? slotSpec = FindStringArg(args, 0);
                if (item != null && slotSpec != null)
                {
                    GunsmithGui.RefreshPartsFromLua(item, slotSpec);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkRefreshQuick", args =>
            {
                Item? item = FindArg<Item>(args);
                string? slotSpec = FindStringArg(args, 0);
                if (item != null && slotSpec != null)
                {
                    GunsmithGui.RefreshQuickFromLua(item, slotSpec);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkIsOpen", args =>
            {
                Item? item = FindArg<Item>(args);
                string? mode = FindStringArg(args, 0);
                bool quickMode = string.Equals(mode, "quick", StringComparison.OrdinalIgnoreCase);
                return item != null && GunsmithGui.IsOpenForItem(item, quickMode);
            });

            hook.Add("GunsmithFrameworkRegisterHiddenQuickSlots", args =>
            {
                string? itemIdentifier = FindStringArg(args, 0);
                string? slotSpec = FindStringArg(args, 1);
                if (itemIdentifier != null && slotSpec != null)
                {
                    GunsmithHiddenQuickSlotsPatch.RegisterHiddenSlots(itemIdentifier, slotSpec);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkRegisterQuickSlotVisibility", args =>
            {
                string? itemIdentifier = FindStringArg(args, 0);
                int slotIndex = FindIntArg(args, 0);
                string? identifierSpec = FindStringArg(args, 1);
                if (itemIdentifier != null && slotIndex >= 0 && identifierSpec != null)
                {
                    GunsmithHiddenQuickSlotsPatch.RegisterVisibleWhenContained(itemIdentifier, slotIndex, identifierSpec);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkRegisterQuickSlotCapacity", args =>
            {
                string? itemIdentifier = FindStringArg(args, 0);
                int maxSlot = FindIntArg(args, 0);
                string quickSlotTags = FindStringArg(args, 1) ?? string.Empty;
                if (itemIdentifier != null && maxSlot >= 0)
                {
                    GunsmithQuickSlotCapacityPatch.RegisterQuickSlotCapacity(itemIdentifier, maxSlot, quickSlotTags);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkRegisterWeaponTags", args =>
            {
                string? tagSpec = FindStringArg(args, 0);
                if (tagSpec != null)
                {
                    GunsmithApi.RegisterWeaponTags(tagSpec);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkClearQuickSlotLayouts", args =>
            {
                Item? item = FindArg<Item>(args);
                if (item != null)
                {
                    GunsmithQuickSlotLayoutPatch.ClearLayouts(item);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkRegisterQuickSlotLayout", args =>
            {
                Item? item = FindArg<Item>(args);
                int slotIndex = FindIntArg(args, 0);
                float anchorX = FindFloatArg(args, 1);
                float anchorY = FindFloatArg(args, 2);
                float offsetX = FindFloatArg(args, 5);
                float offsetY = FindFloatArg(args, 6);
                float rotation = FindFloatArg(args, 7);
                bool hide = FindIntArg(args, 8) != 0;
                if (item != null && slotIndex >= 0)
                {
                    GunsmithQuickSlotLayoutPatch.RegisterLayout(
                        item,
                        slotIndex,
                        new Vector2(anchorX, anchorY),
                        new Vector2(offsetX, offsetY),
                        rotation,
                        hide);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkClearQuickAttachmentBarrelTransforms", args =>
            {
                Item? item = FindArg<Item>(args);
                if (item != null)
                {
                    GunsmithQuickAttachmentBarrelTransforms.ClearTransforms(item);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkRegisterQuickAttachmentBarrelCanvasPoint", args =>
            {
                Item? item = FindArg<Item>(args);
                string? key = FindStringArg(args, 0);
                if (item == null ||
                    string.IsNullOrWhiteSpace(key) ||
                    !TryFindFloatArg(args, 0, out float canvasX) ||
                    !TryFindFloatArg(args, 1, out float canvasY) ||
                    !TryFindFloatArg(args, 2, out float outletOffsetX) ||
                    !TryFindFloatArg(args, 3, out float outletOffsetY) ||
                    !TryFindFloatArg(args, 4, out float rotation))
                {
                    DebugConsole.ThrowError("GunsmithFramework QAT received a malformed barrel canvas payload. Expected item, key, canvasX, canvasY, outletOffsetX, outletOffsetY, rotation.");
                    return null;
                }

                Vector2 canvasPoint = new(canvasX + outletOffsetX, canvasY + outletOffsetY);
                if (!TryCanvasPointToItemLocal(item, canvasPoint, out Vector2 localPosition))
                {
                    DebugConsole.ThrowError(
                        $"GunsmithFramework QAT could not convert a barrel canvas point to item local coordinates. " +
                        $"weapon={item.Prefab.Identifier.Value}, key={key}, canvasPoint={canvasPoint}");
                    return null;
                }

                GunsmithQuickAttachmentBarrelTransforms.RegisterTransform(item, key, localPosition.X, localPosition.Y, rotation);
                return null;
            });

            hook.Add("GunsmithFrameworkBeginQuickSlotMutation", args =>
            {
                Item? item = FindArg<Item>(args);
                if (item != null)
                {
                    GunsmithHiddenQuickSlotsPatch.BeginQuickSlotMutation(item);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkEndQuickSlotMutation", args =>
            {
                Item? item = FindArg<Item>(args);
                if (item != null)
                {
                    GunsmithHiddenQuickSlotsPatch.EndQuickSlotMutation(item);
                }
                return null;
            });

            hook.Add("GunsmithFrameworkIsQuickSlotMutation", args =>
            {
                Item? item = FindArg<Item>(args);
                return item != null && GunsmithHiddenQuickSlotsPatch.IsQuickSlotMutation(item);
            });

            hook.Add("GunsmithFrameworkGetSavedState", args =>
            {
                Item? item = FindArg<Item>(args);
                Barotrauma.Items.Components.GunsmithData? data = item?.GetComponent<Barotrauma.Items.Components.GunsmithData>();
                return data?.SavedState ?? string.Empty;
            });

            hook.Add("GunsmithFrameworkGetNpcPreset", args =>
            {
                Item? item = FindArg<Item>(args);
                return GunsmithNpcPresetPatch.GetPreset(item);
            });

            hook.Add("GunsmithFrameworkRequestState", args =>
            {
                Item? item = FindArg<Item>(args);
                if (item != null)
                {
                    Barotrauma.Items.Components.GunsmithData? data = item.GetComponent<Barotrauma.Items.Components.GunsmithData>();
                    if (data == null)
                    {
                        CallLuaHook("GunsmithFrameworkReceiveState", item, string.Empty);
                    }
                    else if (GameMain.Client != null)
                    {
                        data.RequestStateFromServer();
                    }
                    else
                    {
                        CallLuaHook("GunsmithFrameworkReceiveState", item, data.SavedState);
                    }
                }
                return null;
            });

            hook.Add("GunsmithFrameworkSaveState", args =>
            {
                Item? item = FindArg<Item>(args);
                string? savedState = FindStringArg(args, 0);
                Barotrauma.Items.Components.GunsmithData? data = item?.GetComponent<Barotrauma.Items.Components.GunsmithData>();
                if (data != null && savedState != null)
                {
                    if (GameMain.Client != null)
                    {
                        data.SubmitStateToServer(savedState);
                    }
                    else
                    {
                        data.SavedState = savedState;
                    }
                }
                return null;
            });

            hook.Add("GunsmithFrameworkCanEnsureQuickPartItem", args => GameMain.Client == null);

            hook.Add("GunsmithFrameworkEnsureQuickPartItem", args =>
            {
                Item? item = FindArg<Item>(args);
                int slotIndex = FindIntArg(args, 0, defaultValue: -1);
                string? itemIdentifier = FindStringArg(args, 0);
                return GameMain.Client == null &&
                       item != null &&
                       slotIndex >= 0 &&
                       !string.IsNullOrWhiteSpace(itemIdentifier) &&
                       GunsmithQuickPartItemSpawner.Ensure(item, slotIndex, itemIdentifier, createNetworkEvent: true);
            });
        }

        internal static void CallLuaHook(string hookName, params object[] args)
            => GunsmithLuaHooks.Call(hookName, args);

    }
}
