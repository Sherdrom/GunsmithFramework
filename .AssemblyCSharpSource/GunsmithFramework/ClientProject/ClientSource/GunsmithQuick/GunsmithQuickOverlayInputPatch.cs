namespace GunsmithFramework
{
    [HarmonyPatch]
    internal static class GunsmithQuickOverlayInputPatch
    {
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.UpdateDragging))]
        [HarmonyPrefix]
        private static bool HandleQuickOverlayDraggingBeforeWorldDrop()
        {
            if (GunsmithGui.TryHandleQuickOverlayDragging())
            {
                return false;
            }
            if (GunsmithGui.IsMouseOnQuickBufferInventory && Inventory.DraggingItems.Any())
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.UpdateDragging))]
        [HarmonyPostfix]
        private static void RestoreQuickOverlayDragAfterNativeDragging()
        {
            if (GunsmithQuickDrag.ReconcileAfterNativeDragging())
            {
                SoundPlayer.PlayUISound(GUISoundType.PickItemFail);
            }
        }

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
    }
}
