using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Barotrauma;
using HarmonyLib;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace GunsmithFramework.Tests;

public sealed class GunsmithLifecycleCleanupTests
{
    [Fact]
    public void OwnerResetsClearSharedRegistriesAndAreIdempotent()
    {
        Item item = Uninitialized<Item>();
        Character character = Uninitialized<Character>();

        SeedRegistry(typeof(GunsmithQuickSlotCapacityPatch), "MaxQuickSlotByItemIdentifier", "weapon");
        SeedRegistry(typeof(GunsmithQuickSlotCapacityPatch), "QuickSlotTagsByItemIdentifier", "weapon");
        SeedRegistry(typeof(GunsmithQuickSlotCapacityPatch), "InjectedSlotsByItemIdentifier", "weapon");
        SeedRegistry(typeof(GunsmithQuickAttachmentBarrelTransforms), "RulesByItem", item);
        SeedRegistry(typeof(GunsmithQuickAttachmentBarrelTransforms), "ActiveRuleKeyByItem", item);
        SeedRegistry(typeof(GunsmithQuickAttachmentBarrelTransforms), "ActiveProjectileSelectionByItem", item);
        SeedRegistry(typeof(GunsmithQuickAttachmentBarrelTransforms), "CachedLocalPositions", item);
        SeedRegistry(typeof(GunsmithQuickAttachmentBarrelTransforms), "ReportedMissingRuleSignatureByItem", item);
        SeedRegistry(typeof(GunsmithErgonomicsAimPatch), "runtimes", character);
        SeedNpcPreset(item);

        GunsmithQuickSlotCapacityPatch.Reset();
        GunsmithQuickAttachmentBarrelTransforms.ClearAllTransforms();
        GunsmithErgonomicsAimPatch.Reset();
        GunsmithNpcPresetPatch.Reset();
        GunsmithQuickSlotCapacityPatch.Reset();
        GunsmithQuickAttachmentBarrelTransforms.ClearAllTransforms();
        GunsmithErgonomicsAimPatch.Reset();
        GunsmithNpcPresetPatch.Reset();

        AssertRegistryEmpty(typeof(GunsmithQuickSlotCapacityPatch), "MaxQuickSlotByItemIdentifier");
        AssertRegistryEmpty(typeof(GunsmithQuickSlotCapacityPatch), "QuickSlotTagsByItemIdentifier");
        AssertRegistryEmpty(typeof(GunsmithQuickSlotCapacityPatch), "InjectedSlotsByItemIdentifier");
        AssertRegistryEmpty(typeof(GunsmithQuickAttachmentBarrelTransforms), "RulesByItem");
        AssertRegistryEmpty(typeof(GunsmithQuickAttachmentBarrelTransforms), "ActiveRuleKeyByItem");
        AssertRegistryEmpty(typeof(GunsmithQuickAttachmentBarrelTransforms), "ActiveProjectileSelectionByItem");
        AssertRegistryEmpty(typeof(GunsmithQuickAttachmentBarrelTransforms), "CachedLocalPositions");
        AssertRegistryEmpty(typeof(GunsmithQuickAttachmentBarrelTransforms), "ReportedMissingRuleSignatureByItem");
        AssertRegistryEmpty(typeof(GunsmithErgonomicsAimPatch), "runtimes");
        AssertRegistryEmpty(typeof(GunsmithNpcPresetPatch), "PendingItemPresets");
        Assert.Equal(string.Empty, GunsmithNpcPresetPatch.GetPreset(item));
    }

    [Fact]
    public void SpawnerAndOptionalSelectorResetReleaseGenerationState()
    {
        AddToSet(typeof(GunsmithQuickPartItemSpawner), "PendingQuickPartSpawns", "1:2:part");
        GunsmithQuickPartItemSpawner.BeginQuickSlotMutation = _ => { };
        GunsmithQuickPartItemSpawner.EndQuickSlotMutation = _ => { };

        PropertyInfo selector = typeof(SelectorStub).GetProperty(nameof(SelectorStub.Value))!;
        SetStaticField(typeof(GunsmithQuickAttachmentBarrelSelectorPatch), "harmonyInstance", new Harmony("GunsmithLifecycleCleanupTests"));
        SetStaticField(typeof(GunsmithQuickAttachmentBarrelSelectorPatch), "switchableRangedWeaponType", typeof(SelectorStub));
        SetStaticField(typeof(GunsmithQuickAttachmentBarrelSelectorPatch), "selectorGetter", selector.GetMethod);
        SetStaticField(typeof(GunsmithQuickAttachmentBarrelSelectorPatch), "patchedSelectorSetter", selector.SetMethod);

        GunsmithQuickPartItemSpawner.Reset();
        GunsmithQuickAttachmentBarrelSelectorPatch.Reset();
        GunsmithQuickPartItemSpawner.Reset();
        GunsmithQuickAttachmentBarrelSelectorPatch.Reset();

        AssertRegistryEmpty(typeof(GunsmithQuickPartItemSpawner), "PendingQuickPartSpawns");
        Assert.Null(GunsmithQuickPartItemSpawner.BeginQuickSlotMutation);
        Assert.Null(GunsmithQuickPartItemSpawner.EndQuickSlotMutation);
        Assert.Null(GetStaticField(typeof(GunsmithQuickAttachmentBarrelSelectorPatch), "harmonyInstance"));
        Assert.Null(GetStaticField(typeof(GunsmithQuickAttachmentBarrelSelectorPatch), "switchableRangedWeaponType"));
        Assert.Null(GetStaticField(typeof(GunsmithQuickAttachmentBarrelSelectorPatch), "selectorGetter"));
        Assert.Null(GetStaticField(typeof(GunsmithQuickAttachmentBarrelSelectorPatch), "patchedSelectorSetter"));
    }

    [Fact]
    public void DisposeClearsPackageRuntimeAndOwnerStateAndIsIdempotent()
    {
        AssemblyLoadContext.Default.Resolving += ResolveGameDependency;
        try
        {
            RunDisposeTest();
        }
        finally
        {
            AssemblyLoadContext.Default.Resolving -= ResolveGameDependency;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunDisposeTest()
    {
        Item item = Uninitialized<Item>();
        GunsmithRuntimeStates.Set(item, new GunsmithRuntimeState
        {
            Signature = "seed",
            ManagedItemIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "part" }
        });
        SeedRegistry(typeof(GunsmithQuickSlotCapacityPatch), "MaxQuickSlotByItemIdentifier", "weapon");
        GunsmithQuickPartItemSpawner.BeginQuickSlotMutation = _ => { };
        Type regularPackage = typeof(ContentPackage).Assembly.GetType("Barotrauma.RegularPackage", throwOnError: true)!;
        typeof(global::GunsmithFramework.GunsmithFramework).GetProperty(nameof(global::GunsmithFramework.GunsmithFramework.Package))!
            .SetValue(null, RuntimeHelpers.GetUninitializedObject(regularPackage));

        var plugin = new global::GunsmithFramework.GunsmithFramework();
        plugin.Dispose();
        plugin.Dispose();

        Assert.Null(global::GunsmithFramework.GunsmithFramework.Package);
        Assert.False(GunsmithRuntimeStates.TryGet(item, out _));
        Assert.False(GunsmithRuntimeStates.HasManagedRuntimeItems);
        AssertRegistryEmpty(typeof(GunsmithQuickSlotCapacityPatch), "MaxQuickSlotByItemIdentifier");
        Assert.Null(GunsmithQuickPartItemSpawner.BeginQuickSlotMutation);
    }

    private static void SeedNpcPreset(Item item)
    {
        object presets = GetStaticField(typeof(GunsmithNpcPresetPatch), "Presets")!;
        Type presetType = presets.GetType().GetGenericArguments()[1];
        object preset = Activator.CreateInstance(
            presetType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { "seed" },
            culture: null)!;
        presets.GetType().GetMethod("Add")!.Invoke(presets, new[] { item, preset });

        IList pending = (IList)GetStaticField(typeof(GunsmithNpcPresetPatch), "PendingItemPresets")!;
        Type pendingType = pending.GetType().GetGenericArguments()[0];
        pending.Add(Activator.CreateInstance(
            pendingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { "weapon", "seed" },
            culture: null)!);
    }

    private static void SeedRegistry(Type owner, string fieldName, object key)
    {
        object registry = GetStaticField(owner, fieldName)!;
        Type valueType = registry.GetType().GetGenericArguments()[1];
        object value = valueType == typeof(int)
            ? 1
            : valueType == typeof(string)
                ? "seed"
                : Activator.CreateInstance(valueType, nonPublic: true)!;
        registry.GetType().GetProperty("Item")!.SetValue(registry, value, new[] { key });
    }

    private static void AddToSet(Type owner, string fieldName, object value)
    {
        object set = GetStaticField(owner, fieldName)!;
        set.GetType().GetMethod("Add")!.Invoke(set, new[] { value });
    }

    private static void AssertRegistryEmpty(Type owner, string fieldName)
    {
        object registry = GetStaticField(owner, fieldName)!;
        Assert.Equal(0, (int)registry.GetType().GetProperty("Count")!.GetValue(registry)!);
    }

    private static object? GetStaticField(Type owner, string fieldName)
        => owner.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null);

    private static void SetStaticField(Type owner, string fieldName, object? value)
        => owner.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(null, value);

    private static T Uninitialized<T>() where T : class
        => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static Assembly? ResolveGameDependency(AssemblyLoadContext context, AssemblyName name)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, name.Name + ".dll");
            if (File.Exists(path))
            {
                return context.LoadFromAssemblyPath(path);
            }
        }

        return null;
    }

    private sealed class SelectorStub
    {
        public int Value { get; set; }
    }

}
