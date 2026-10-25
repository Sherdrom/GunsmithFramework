using System.Reflection;
using System.Runtime.CompilerServices;
using Barotrauma;
using GunsmithFramework;
using Xunit;

namespace GunsmithSharedTest;

public sealed class GunsmithRuntimeStatsTests
{
    [Theory]
    [InlineData(StatTypes.TeamAttackMultiplier, 0.0f, -1.5f, -1.0f)]
    [InlineData(StatTypes.TeamAttackMultiplier, -0.4f, -0.8f, -1.0f)]
    [InlineData(StatTypes.TeamAttackMultiplier, 0.5f, -1.2f, -0.7f)]
    [InlineData(StatTypes.TeamAttackMultiplier, 0.0f, -1.0f, -1.0f)]
    [InlineData(StatTypes.RangedAttackMultiplier, 0.2f, 0.3f, 0.5f)]
    [InlineData(StatTypes.RangedAttackSpeed, 0.0f, -2.0f, -1.0f)]
    [InlineData(StatTypes.WeaponsSkillBonus, -5.0f, -15.0f, -20.0f)]
    [InlineData(StatTypes.WeaponsSkillOverride, 0.0f, -15.0f, -15.0f)]
    [InlineData(StatTypes.ExtraMissionCount, 0.0f, -3.0f, -3.0f)]
    [InlineData(StatTypes.TeamAttackMultiplier, -2.0f, 0.0f, -2.0f)]
    public void RuntimeClampsPercentageAfterCombiningWithCharacterStats(
        StatTypes statType, float original, float modifier, float expected)
    {
        Character character = (Character)RuntimeHelpers.GetUninitializedObject(typeof(Character));
        GunsmithRuntimeState state = GunsmithRuntimeStates.CreateState(
            "stats", $"{statType}={modifier.ToString(System.Globalization.CultureInfo.InvariantCulture)}", "");
        Assert.Equal(modifier, state.Stats.Values.GetValueOrDefault(statType));

        Type patch = typeof(GunsmithRuntimeEffectsPatch);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        object cache = Activator.CreateInstance(
            patch.GetNestedType("HeldGunsmithStateCache", BindingFlags.NonPublic)!,
            Timing.TotalTime, true, state)!;
        patch.GetMethod("SetHeldStateCache", flags)!.Invoke(null, [character, cache]);
        try
        {
            object[] args = [character, statType, original];
            patch.GetMethod("ApplyGunsmithCharacterStats", flags)!.Invoke(null, args);
            Assert.Equal(expected, Assert.IsType<float>(args[2]), precision: 5);
        }
        finally
        {
            GunsmithRuntimeEffectsPatch.ClearCaches();
        }
    }

    [Fact]
    public void RuntimeLeavesCharactersWithoutGunsmithStatsUnchanged()
    {
        Character character = (Character)RuntimeHelpers.GetUninitializedObject(typeof(Character));
        object[] args = [character, StatTypes.TeamAttackMultiplier, -2.0f];
        typeof(GunsmithRuntimeEffectsPatch).GetMethod(
            "ApplyGunsmithCharacterStats", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);

        Assert.Equal(-2.0f, Assert.IsType<float>(args[2]));
        GunsmithRuntimeEffectsPatch.ClearCaches();
    }
}
