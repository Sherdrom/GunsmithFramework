using System.Runtime.CompilerServices;
using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Xunit;

namespace GunsmithFramework.Tests;

public sealed class GunsmithBarrelFallbackTests
{
    private static bool hasLowerRailBarrel;
    private static int warnings;

    [Fact]
    public void EmptyLowerRailFallsBackSilentlyButMissingInstalledBarrelStillWarns()
    {
        Item item = (Item)RuntimeHelpers.GetUninitializedObject(typeof(Item));
        RangedWeapon weapon = (RangedWeapon)RuntimeHelpers.GetUninitializedObject(typeof(RangedWeapon));
        AccessTools.Field(typeof(Item), "componentsByType").SetValue(item,
            new Dictionary<Type, List<ItemComponent>> { [typeof(RangedWeapon)] = [weapon] });
        var harmony = new Harmony(nameof(GunsmithBarrelFallbackTests));
        harmony.Patch(AccessTools.Method(typeof(GunsmithLuaHooks), "Call"),
            prefix: new HarmonyMethod(typeof(GunsmithBarrelFallbackTests), nameof(ReadInstalledBarrel)));
        harmony.Patch(AccessTools.Method(typeof(GunsmithQuickAttachmentBarrelTransforms), "ReportMissingActiveRuleOnce"),
            prefix: new HarmonyMethod(typeof(GunsmithBarrelFallbackTests), nameof(CountWarning)));
        hasLowerRailBarrel = false;
        warnings = 0;
        try
        {
            GunsmithQuickAttachmentBarrelTransforms.ApplySelectedProjectile(item, 0);
            GunsmithQuickAttachmentBarrelTransforms.RegisterTransform(item, "primary", 100, 20, 0);
            GunsmithQuickAttachmentBarrelTransforms.ApplySelectedProjectile(item, 1);
            AssertPosition(item, weapon, new Vector2(100, 20));
            Assert.Equal(0, warnings);

            hasLowerRailBarrel = true;
            GunsmithQuickAttachmentBarrelTransforms.ApplySelectedProjectile(item, 1);
            AssertPosition(item, weapon, new Vector2(100, 20));
            Assert.Equal(1, warnings);

            GunsmithQuickAttachmentBarrelTransforms.RegisterTransform(item, "lower_rail", 60, -10, 0);
            GunsmithQuickAttachmentBarrelTransforms.ApplySelectedProjectile(item, 1);
            AssertPosition(item, weapon, new Vector2(60, -10));
            Assert.Equal(1, warnings);

            GunsmithQuickAttachmentBarrelTransforms.ApplySelectedProjectile(item, 0);
            AssertPosition(item, weapon, new Vector2(100, 20));
        }
        finally
        {
            harmony.UnpatchSelf();
            GunsmithQuickAttachmentBarrelTransforms.ClearTransforms(item);
        }
    }

    private static bool ReadInstalledBarrel(string hookName, ref object? __result)
    {
        Assert.Equal("GunsmithFrameworkHasLowerRailBarrel", hookName);
        __result = hasLowerRailBarrel;
        return false;
    }

    private static bool CountWarning()
    {
        warnings++;
        return false;
    }

    private static void AssertPosition(Item item, RangedWeapon weapon, Vector2 expected)
    {
        Assert.True(GunsmithQuickAttachmentBarrelTransforms.TryGetCurrentLocalPosition(item, out Vector2 actual));
        Assert.Equal(expected, actual);
        Vector2 barrel = XMLExtensions.ParseVector2(weapon.BarrelPos);
        Assert.Equal(expected.X, barrel.X, precision: 3);
        Assert.Equal(expected.Y, barrel.Y, precision: 3);
    }
}
