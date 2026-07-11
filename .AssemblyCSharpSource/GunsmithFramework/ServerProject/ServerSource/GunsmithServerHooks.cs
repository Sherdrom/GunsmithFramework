using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using static GunsmithFramework.GunsmithHookArgs;

namespace GunsmithFramework
{
    public static class GunsmithServerHooks
    {
        public static void RegisterLuaHooks(Barotrauma.LuaCs.Compatibility.ILuaCsHook hook)
        {
            GunsmithLuaHooks.Add(hook, "GunsmithFrameworkRegisterQuickSlotCapacity", args =>
            {
                string? itemIdentifier = FindStringArg(args, 0);
                int maxSlot = FindIntArg(args, 0, defaultValue: -1);
                string quickSlotTags = FindStringArg(args, 1) ?? string.Empty;
                if (itemIdentifier != null && maxSlot >= 0)
                {
                    GunsmithQuickSlotCapacityPatch.RegisterQuickSlotCapacity(itemIdentifier, maxSlot, quickSlotTags);
                }
                return null;
            });

            GunsmithLuaHooks.Add(hook, "GunsmithFrameworkApplyRuntimeState", args =>
            {
                Item? item = FindArg<Item>(args);
                string? signature = FindStringArg(args, 0);
                string? statsSpec = FindStringArg(args, 1);
                string? managedItemSpec = FindStringArg(args, 2);
                return item != null &&
                       signature != null &&
                       GunsmithRuntimeStates.ApplyFromLua(item, signature, statsSpec ?? string.Empty, managedItemSpec ?? string.Empty);
            });

            GunsmithLuaHooks.Add(hook, "GunsmithFrameworkClearRuntimeState", args =>
            {
                Item? item = FindArg<Item>(args);
                if (item != null)
                {
                    GunsmithRuntimeStates.Remove(item);
                    GunsmithQuickAttachmentBarrelTransforms.ClearTransforms(item);
                }
                return null;
            });

            GunsmithLuaHooks.Add(hook, "GunsmithFrameworkClearQuickAttachmentBarrelTransforms", args =>
            {
                Item? item = FindArg<Item>(args);
                if (item != null)
                {
                    GunsmithQuickAttachmentBarrelTransforms.ClearTransforms(item);
                }
                return null;
            });

            GunsmithLuaHooks.Add(hook, "GunsmithFrameworkRegisterQuickAttachmentBarrelCanvasPoint", args =>
            {
                Item? item = FindArg<Item>(args);
                string? key = FindStringArg(args, 0);
                if (item == null ||
                    string.IsNullOrWhiteSpace(key) ||
                    !TryFindFloatArg(args, 0, out float canvasX) ||
                    !TryFindFloatArg(args, 1, out float canvasY) ||
                    !TryFindFloatArg(args, 2, out float outletOffsetX) ||
                    !TryFindFloatArg(args, 3, out float outletOffsetY) ||
                    !TryFindFloatArg(args, 4, out float rotation) ||
                    !TryFindFloatArg(args, 5, out float canvasWidth) ||
                    !TryFindFloatArg(args, 6, out float canvasHeight) ||
                    !TryFindFloatArg(args, 7, out float worldScale) ||
                    !TryFindFloatArg(args, 8, out float worldRotation) ||
                    !TryFindFloatArg(args, 9, out float worldOffsetX) ||
                    !TryFindFloatArg(args, 10, out float worldOffsetY))
                {
                    DebugConsole.ThrowError("GunsmithFramework QAT received a malformed server barrel canvas payload. Expected item, key, canvasX, canvasY, outletOffsetX, outletOffsetY, rotation, canvasWidth, canvasHeight, worldScale, worldRotation, worldOffsetX, worldOffsetY.");
                    return null;
                }

                Vector2 canvasPoint = new(canvasX + outletOffsetX, canvasY + outletOffsetY);
                if (!TryCanvasPointToItemLocal(item, canvasPoint, new Vector2(canvasWidth, canvasHeight), worldScale, worldRotation, new Vector2(worldOffsetX, worldOffsetY), out Vector2 localPosition))
                {
                    DebugConsole.ThrowError(
                        $"GunsmithFramework QAT could not convert a server barrel canvas point to item local coordinates. " +
                        $"weapon={item.Prefab.Identifier.Value}, key={key}, canvasPoint={canvasPoint}");
                    return null;
                }

                GunsmithQuickAttachmentBarrelTransforms.RegisterTransform(item, key, localPosition.X, localPosition.Y, rotation);
                return null;
            });

            GunsmithLuaHooks.Add(hook, "GunsmithFrameworkGetSavedState", args =>
            {
                Item? item = FindArg<Item>(args);
                return GunsmithDataAccess.GetSavedState(item);
            });

            GunsmithLuaHooks.Add(hook, "GunsmithFrameworkGetNpcPreset", args =>
            {
                Item? item = FindArg<Item>(args);
                return GunsmithNpcPresetPatch.GetPreset(item);
            });

            GunsmithLuaHooks.Add(hook, "GunsmithFrameworkSaveState", args =>
            {
                Item? item = FindArg<Item>(args);
                string? savedState = FindStringArg(args, 0);
                if (item != null && savedState != null && GunsmithDataAccess.SetSavedState(item, savedState))
                {
                    GunsmithDataAccess.BroadcastState(item);
                }
                return null;
            });

            GunsmithLuaHooks.Add(hook, "GunsmithFrameworkCanEnsureQuickPartItem", args => true);

            GunsmithLuaHooks.Add(hook, "GunsmithFrameworkEnsureQuickPartItem", args =>
            {
                Item? item = FindArg<Item>(args);
                int slotIndex = FindIntArg(args, 0, defaultValue: -1);
                string? itemIdentifier = FindStringArg(args, 0);
                return item != null &&
                       slotIndex >= 0 &&
                       !string.IsNullOrWhiteSpace(itemIdentifier) &&
                       GunsmithQuickPartItemSpawner.Ensure(item, slotIndex, itemIdentifier, createNetworkEvent: true);
            });
        }

        private static bool TryCanvasPointToItemLocal(
            Item item,
            Vector2 canvasPoint,
            Vector2 canvasSize,
            float worldScale,
            float worldRotationDegrees,
            Vector2 worldOffset,
            out Vector2 localPosition)
        {
            localPosition = Vector2.Zero;
            if (item == null ||
                !IsFinite(canvasPoint) ||
                !IsFinite(canvasSize) ||
                !float.IsFinite(worldScale) ||
                !float.IsFinite(worldRotationDegrees) ||
                !IsFinite(worldOffset))
            {
                return false;
            }

            Vector2 relativeOrigin = item.Prefab.Sprite?.RelativeOrigin ?? new Vector2(0.5f, 0.5f);
            Vector2 canvasOrigin = new(canvasSize.X * relativeOrigin.X, canvasSize.Y * relativeOrigin.Y);
            Vector2 delta = (canvasPoint - canvasOrigin) * Math.Max(worldScale, 0.01f);
            float radians = MathHelper.ToRadians(worldRotationDegrees);
            Vector2 textureOffset = worldOffset + Rotate(delta, radians);
            localPosition = new Vector2(textureOffset.X, -textureOffset.Y);
            return IsFinite(localPosition);
        }

        private static Vector2 Rotate(Vector2 value, float radians)
        {
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);
            return new Vector2(
                value.X * cos - value.Y * sin,
                value.X * sin + value.Y * cos);
        }

        private static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
