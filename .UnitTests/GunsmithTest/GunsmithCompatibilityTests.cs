using System.Reflection;
using System.Runtime.Loader;
using Barotrauma.Items.Components;
using HarmonyLib;
using Xunit;

namespace GunsmithCompatibilityTest;

[CollectionDefinition("Compatibility patches", DisableParallelization = true)]
public sealed class CompatibilityPatchCollection { }

[Collection("Compatibility patches")]
public sealed class GunsmithCompatibilityTests
{
    [Fact]
    public void AllHarmonyPatchTargetsResolveAgainstTheGameReferences()
    {
        MethodInfo resolveTarget = AccessTools.Method("HarmonyLib.PatchTools:GetOriginalMethod");
        Assert.NotNull(resolveTarget);
        foreach (Type type in typeof(GunsmithFramework.GunsmithFramework).Assembly.GetTypes()
                     .Where(type => type.IsDefined(typeof(HarmonyPatch))))
        {
            if (AccessTools.DeclaredMethod(type, "TargetMethod") is { } targetMethod)
            {
                Assert.IsAssignableFrom<MethodBase>(targetMethod.Invoke(null, null));
                continue;
            }

            HarmonyMethod container = HarmonyMethodExtensions.GetMergedFromType(type);
            if (container.declaringType != null) { AssertTarget(container, type.FullName!); }
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type)
                         .Where(method => method.IsDefined(typeof(HarmonyPatch))))
            {
                AssertTarget(container.Merge(HarmonyMethodExtensions.GetMergedFromMethod(method)), method.Name);
            }
        }

        void AssertTarget(HarmonyMethod patch, string name)
        {
            patch.methodType ??= MethodType.Normal;
            Assert.True(resolveTarget.Invoke(null, [patch]) is MethodBase, $"Unresolved patch target: {name}");
        }
    }

    [Fact]
    public void AllHarmonyPatchesRegisterAgainstTheGameReferences()
    {
        AssemblyLoadContext.Default.Resolving += ResolveGameDependency;
        var harmony = new Harmony("GunsmithFramework.CompatibilityTests");
        try
        {
            Exception? patchError = Record.Exception(() =>
            {
                harmony.PatchAll(typeof(GunsmithFramework.GunsmithFramework).Assembly);
                Assert.Contains(
                    AccessTools.Method(typeof(RangedWeapon), nameof(RangedWeapon.Use), [typeof(float), typeof(Barotrauma.Character)]),
                    harmony.GetPatchedMethods());
            });
            Exception? cleanupError = Record.Exception(harmony.UnpatchSelf);
            Assert.Null(patchError);
            Assert.Null(cleanupError);
        }
        finally
        {
            AssemblyLoadContext.Default.Resolving -= ResolveGameDependency;
        }
    }

    private static Assembly? ResolveGameDependency(AssemblyLoadContext context, AssemblyName name)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, name.Name + ".dll");
            if (File.Exists(path)) { return context.LoadFromAssemblyPath(path); }
        }
        return null;
    }
}
