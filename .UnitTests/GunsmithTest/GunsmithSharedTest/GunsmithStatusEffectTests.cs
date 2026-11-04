using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Xml.Linq;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Xunit;

namespace GunsmithFramework.Tests;

public sealed class GunsmithStatusEffectTests
{
    [Fact]
    public void ManagedPartDoesNotCancelPropertiesOnMixedTargets()
    {
        AssemblyLoadContext.Default.Resolving += ResolveGameDependency;
        try { ApplyMixedTargetEffect(); }
        finally { AssemblyLoadContext.Default.Resolving -= ResolveGameDependency; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ApplyMixedTargetEffect()
    {
        Item weapon = Uninitialized<Item>();
        Item part = Uninitialized<Item>();
        ItemPrefab prefab = Uninitialized<ItemPrefab>();
        AccessTools.Field(typeof(Prefab), nameof(Prefab.Identifier)).SetValue(prefab, "managed_part".ToIdentifier());
        AccessTools.Field(typeof(MapEntity), nameof(MapEntity.Prefab)).SetValue(part, prefab);
        AccessTools.Field(typeof(Item), "parentInventory").SetValue(part, new Inventory(weapon, 1));
        part.SerializableProperties = new();
        GunsmithRuntimeStates.Set(weapon, GunsmithRuntimeStates.CreateState("state", ""));
        var probe = new PropertyTarget();
        StatusEffect effect = StatusEffect.Load(new ContentXElement(null,
            XElement.Parse("<StatusEffect type='OnUse' target='This' setvalue='true' Value='7' />")), "mixed-target regression");
        effect.Afflictions.Add(Uninitialized<Affliction>());

        var harmony = new Harmony(nameof(GunsmithStatusEffectTests));
        try
        {
            // Include any framework StatusEffect patches so the old blanket prefixes fail this test.
            foreach (Type type in typeof(GunsmithRuntimeEffectsPatch).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes<HarmonyPatch>().Any(patch => patch.info.declaringType == typeof(StatusEffect)))
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
            }
            effect.Apply(ActionType.OnUse, 1, null!, new ISerializableEntity[] { part, probe }, Vector2.Zero);
            Assert.Equal(7f, probe.Value);
        }
        finally
        {
            harmony.UnpatchSelf();
            GunsmithRuntimeStates.Remove(weapon);
        }
    }

    private sealed class PropertyTarget : ISerializableEntity
    {
        public string Name => "ordinary target";
        [Serialize(0f, IsPropertySaveable.No)]
        public float Value { get; set; }
        public Dictionary<Identifier, SerializableProperty> SerializableProperties { get; set; }
        public PropertyTarget()
            => SerializableProperties = SerializableProperty.DeserializeProperties(this, new ContentXElement(null, new XElement("Target")));
    }

    private static T Uninitialized<T>() where T : class
        => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

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
