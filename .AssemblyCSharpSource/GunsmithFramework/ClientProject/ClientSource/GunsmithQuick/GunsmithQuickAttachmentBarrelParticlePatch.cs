using Barotrauma.Items.Components;
using FarseerPhysics;

namespace GunsmithFramework
{
    [HarmonyPatch]
    public static class GunsmithQuickAttachmentBarrelParticlePatch
    {
        private const string QuickAttachmentBarrelParticleTag = "Gunsmith_BarrelParticle";
        private static readonly Identifier QuickAttachmentBarrelParticleIdentifier = QuickAttachmentBarrelParticleTag.ToIdentifier();

        private static MethodBase? TargetMethod()
        {
            MethodInfo? method = AccessTools.Method(
                typeof(StatusEffect),
                "ApplyProjSpecific",
                new[]
                {
                    typeof(float),
                    typeof(Entity),
                    typeof(IReadOnlyList<ISerializableEntity>),
                    typeof(Hull),
                    typeof(Vector2),
                    typeof(bool)
                });
            if (method == null)
            {
                DebugConsole.ThrowError("GunsmithFramework QAT failed to find StatusEffect.ApplyProjSpecific for barrel particle origin patch.");
            }
            return method;
        }

        [HarmonyPrefix]
        private static void UseQuickAttachmentBarrelPosition(StatusEffect __instance, Entity entity, ref Vector2 worldPosition)
        {
            if (!HasQuickAttachmentBarrelTag(__instance))
            {
                return;
            }

            if (entity is not Item item)
            {
                DebugConsole.ThrowError($"GunsmithFramework QAT barrel particle StatusEffect was applied by a non-item entity. entity={entity?.ToString() ?? "null"}");
                return;
            }

            RangedWeapon? rangedWeapon = item.GetComponent<RangedWeapon>();
            if (rangedWeapon == null)
            {
                DebugConsole.ThrowError($"GunsmithFramework QAT barrel particle StatusEffect was applied by a non-ranged item. item={item.Prefab.Identifier.Value}");
                return;
            }

            if (!TryGetBarrelDrawPosition(item, rangedWeapon, out Vector2 position))
            {
                return;
            }

            worldPosition = position;
        }

        private static bool TryGetBarrelDrawPosition(Item item, RangedWeapon rangedWeapon, out Vector2 position)
        {
            position = Vector2.Zero;

            if (!GunsmithQuickAttachmentBarrelTransforms.TryGetCurrentLocalPosition(item, out Vector2 localDisplayPosition))
            {
                localDisplayPosition = XMLExtensions.ParseVector2(rangedWeapon.BarrelPos);
            }

            if (!GunsmithQuickTransformMath.IsFinite(localDisplayPosition) || !float.IsFinite(item.Scale))
            {
                DebugConsole.ThrowError(
                    $"GunsmithFramework QAT produced an invalid barrel particle payload. " +
                    $"item={item.Prefab.Identifier.Value}, barrelPos={rangedWeapon.BarrelPos}, itemScale={item.Scale}");
                return false;
            }

            if (!GunsmithQuickTransformMath.TryItemLocalToWorldPosition(item, localDisplayPosition, drawPosition: true, out position))
            {
                DebugConsole.ThrowError(
                    $"GunsmithFramework QAT produced an invalid barrel particle position. " +
                    $"item={item.Prefab.Identifier.Value}, position={position}, barrelPos={rangedWeapon.BarrelPos}");
                return false;
            }

            return true;
        }

        private static bool HasQuickAttachmentBarrelTag(StatusEffect statusEffect)
            => statusEffect.HasTag(QuickAttachmentBarrelParticleIdentifier);
    }
}
