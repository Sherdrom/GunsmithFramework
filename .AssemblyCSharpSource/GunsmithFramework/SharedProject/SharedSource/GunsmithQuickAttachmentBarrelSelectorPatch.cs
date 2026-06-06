using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;
using System;
using System.Reflection;

namespace GunsmithFramework
{
    public static class GunsmithQuickAttachmentBarrelSelectorPatch
    {
        private const string VceTypeName = "Barotrauma.Items.Components.SwitchableRangedWeapon";
        private const string SelectorPropertyName = "currentProjectileSelected";

        private static readonly object PatchLock = new();
        private static Harmony? harmonyInstance;
        private static Type? switchableRangedWeaponType;
        private static MethodInfo? selectorGetter;
        private static MethodInfo? patchedSelectorSetter;

        public static void PatchOptionalVce(Harmony harmony)
        {
            harmonyInstance = harmony;
            Type? type = AccessTools.TypeByName(VceTypeName);
            if (type == null)
            {
                return;
            }

            TryPatchSelectorType(type);
        }

        public static bool TryGetSelectedProjectile(Item item, out int selectedProjectile)
        {
            selectedProjectile = 0;
            if (item == null || item.Removed)
            {
                return false;
            }

            foreach (ItemComponent component in item.GetComponents<ItemComponent>())
            {
                if (!TryEnsureSelectorBridgeForComponent(component))
                {
                    continue;
                }

                return TryReadSelectedProjectile(component, out selectedProjectile);
            }

            return false;
        }

        private static bool TryEnsureSelectorBridgeForComponent(ItemComponent component)
        {
            if (component == null)
            {
                return false;
            }

            Type componentType = component.GetType();
            if (switchableRangedWeaponType != null &&
                selectorGetter != null &&
                switchableRangedWeaponType.IsInstanceOfType(component))
            {
                return true;
            }

            PropertyInfo? property = componentType.GetProperty(
                SelectorPropertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
            {
                return false;
            }

            return TryPatchSelectorType(componentType);
        }

        private static bool TryPatchSelectorType(Type type)
        {
            lock (PatchLock)
            {
                if (switchableRangedWeaponType == type &&
                    selectorGetter != null &&
                    patchedSelectorSetter != null)
                {
                    return true;
                }

                if (!typeof(RangedWeapon).IsAssignableFrom(type))
                {
                    DebugConsole.ThrowError($"GunsmithFramework QAT VCE bridge found selector property on non-ranged type. type={type.FullName}");
                    return false;
                }

                if (harmonyInstance == null)
                {
                    DebugConsole.ThrowError($"GunsmithFramework QAT VCE bridge found {type.FullName}, but Harmony was not initialized.");
                    return false;
                }

                PropertyInfo? property = type.GetProperty(
                    SelectorPropertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo? getter = property?.GetGetMethod(nonPublic: true);
                MethodInfo? setter = property?.GetSetMethod(nonPublic: true);
                if (property == null || getter == null || setter == null)
                {
                    DebugConsole.ThrowError($"GunsmithFramework QAT VCE bridge found {type.FullName}, but property {SelectorPropertyName} getter/setter is missing.");
                    return false;
                }

                MethodInfo? postfix = typeof(GunsmithQuickAttachmentBarrelSelectorPatch).GetMethod(
                    nameof(AfterCurrentProjectileSelectedChanged),
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (postfix == null)
                {
                    DebugConsole.ThrowError("GunsmithFramework QAT VCE bridge failed to find its selector postfix method.");
                    return false;
                }

                switchableRangedWeaponType = type;
                selectorGetter = getter;
                if (patchedSelectorSetter != setter)
                {
                    harmonyInstance.Patch(setter, postfix: new HarmonyMethod(postfix));
                    patchedSelectorSetter = setter;
                }
                return true;
            }
        }

        private static void AfterCurrentProjectileSelectedChanged(object __instance)
        {
            if (__instance is not RangedWeapon rangedWeapon)
            {
                DebugConsole.ThrowError($"GunsmithFramework QAT VCE bridge selector postfix received a non-ranged instance. instance={__instance?.GetType().FullName ?? "null"}");
                return;
            }

            if (!TryReadSelectedProjectile(__instance, out int selectedProjectile))
            {
                return;
            }

            GunsmithQuickAttachmentBarrelTransforms.ApplySelectedProjectile(rangedWeapon.Item, selectedProjectile);
        }

        private static bool TryReadSelectedProjectile(object instance, out int selectedProjectile)
        {
            selectedProjectile = 0;
            MethodInfo? getter = selectorGetter;
            if (getter == null || !getter.DeclaringType!.IsInstanceOfType(instance))
            {
                if (!TryPatchSelectorType(instance.GetType()))
                {
                    return false;
                }
                getter = selectorGetter;
                if (getter == null)
                {
                    DebugConsole.ThrowError($"GunsmithFramework QAT VCE bridge could not resolve {SelectorPropertyName} getter for {instance.GetType().FullName}.");
                    return false;
                }
            }

            object? value = getter.Invoke(instance, null);
            if (value is int selected)
            {
                selectedProjectile = selected;
                return true;
            }

            DebugConsole.ThrowError(
                $"GunsmithFramework QAT VCE bridge expected {SelectorPropertyName} to return int, " +
                $"but got {value?.GetType().FullName ?? "null"}.");
            return false;
        }
    }
}
