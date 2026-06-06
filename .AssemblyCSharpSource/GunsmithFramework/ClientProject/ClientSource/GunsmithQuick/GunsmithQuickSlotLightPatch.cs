using Barotrauma.Items.Components;
using FarseerPhysics;

namespace GunsmithFramework
{
    [HarmonyPatch]
    public static class GunsmithQuickSlotLightPatch
    {
        private static readonly HashSet<LightComponent> ManagedLights = new();

        internal static void ClearItemState(Item item)
        {
            if (item == null) { return; }

            List<LightComponent> matchingLights = new();
            foreach (LightComponent lightComponent in ManagedLights)
            {
                if (ReferenceEquals(lightComponent.Item, item))
                {
                    matchingLights.Add(lightComponent);
                }
            }

            foreach (LightComponent lightComponent in matchingLights)
            {
                RestoreNativeLightTransform(lightComponent);
            }
        }

        [HarmonyPatch(typeof(LightComponent), nameof(LightComponent.SetLightSourceTransform))]
        [HarmonyPostfix]
        private static void ApplyQuickAttachmentLightTransform(LightComponent __instance)
            => ApplyTransform(__instance);

        [HarmonyPatch(typeof(LightComponent), nameof(LightComponent.Update))]
        [HarmonyPostfix]
        private static void ApplyQuickAttachmentLightTransformAfterUpdate(LightComponent __instance)
            => ApplyTransform(__instance);

        private static void ApplyTransform(LightComponent __instance)
        {
            if (__instance?.Light == null)
            {
                return;
            }

            Item lightItem = __instance.Item;
            if (!GunsmithQuickAttachmentTransformService.TryGetTransform(lightItem, out GunsmithQuickAttachmentTransform transform))
            {
                RestoreNativeLightTransform(__instance);
                return;
            }

            ManagedLights.Add(__instance);
            __instance.Light.ParentSub = transform.Submarine;
            Vector2 lightDrawPosition = transform.DrawPosition + TransformLightOffset(__instance, transform);
            PhysicsBody? parentBody = transform.WeaponItem.body;
            if (parentBody != null)
            {
                __instance.ParentBody = parentBody;
                __instance.Light.ParentBody = parentBody;
                __instance.Light.OffsetFromBody = lightDrawPosition - parentBody.DrawPosition;
            }
            else
            {
                __instance.ParentBody = null;
                __instance.Light.ParentBody = null;
                __instance.Light.OffsetFromBody = Vector2.Zero;
                __instance.Light.Position = transform.Submarine == null
                    ? lightDrawPosition
                    : lightDrawPosition - transform.Submarine.DrawPosition;
            }

            if (__instance.IsOn && lightItem.Condition > 0.0f)
            {
                __instance.Light.Enabled = true;
            }
            __instance.Light.Rotation = MathF.Atan2(transform.Direction.Y, transform.Direction.X);
            __instance.Light.LightSpriteEffect = transform.FacingDirection >= 0.0f ? SpriteEffects.None : SpriteEffects.FlipVertically;
        }

        private static Vector2 TransformLightOffset(LightComponent lightComponent, GunsmithQuickAttachmentTransform transform)
        {
            Vector2 offset = lightComponent.LightOffset * lightComponent.Item.Scale;
            if (offset == Vector2.Zero)
            {
                return Vector2.Zero;
            }

            float rotation = MathF.Atan2(transform.Direction.Y, transform.Direction.X);
            float sin = MathF.Sin(rotation);
            float cos = MathF.Cos(rotation);
            return new Vector2(offset.X * cos - offset.Y * sin, offset.X * sin + offset.Y * cos);
        }

        private static void RestoreNativeLightTransform(LightComponent lightComponent)
        {
            if (!ManagedLights.Remove(lightComponent))
            {
                return;
            }

            lightComponent.ParentBody = null;
            if (lightComponent.Light != null)
            {
                lightComponent.Light.ParentBody = null;
                lightComponent.Light.OffsetFromBody = Vector2.Zero;
            }
            lightComponent.SetLightSourceTransform();
        }
    }
}
