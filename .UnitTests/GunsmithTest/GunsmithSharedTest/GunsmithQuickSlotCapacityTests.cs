using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Xml.Linq;
using Barotrauma;
using Barotrauma.Items.Components;
using GunsmithFramework;
using HarmonyLib;
using Xunit;

namespace GunsmithFramework.Tests;

public sealed class GunsmithQuickSlotCapacityTests : IDisposable
{
    public GunsmithQuickSlotCapacityTests() => AssemblyLoadContext.Default.Resolving += ResolveGameDependency;
    public void Dispose() => AssemblyLoadContext.Default.Resolving -= ResolveGameDependency;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingRailSlotsActivateCustomAttachmentsWithOrWithoutExpansion(bool hasStockSlot)
    {
        XElement root = XElement.Parse("""
            <Item>
              <GunsmithData quickslotstart="2" />
              <ItemContainer capacity="1">
                <Containable items="556mag" setactive="false" />
                <SubContainer capacity="1"><Containable tags="deep_grip" hide="true" /></SubContainer>
                <SubContainer capacity="1"><Containable items="flashlight,deep_laser" hide="true" itempos="30,6" /></SubContainer>
                <SubContainer capacity="1"><Containable items="flashlight,deep_laser" hide="true" itempos="30,6" /></SubContainer>
                <SubContainer capacity="1"><Containable items="holographic_sight" /></SubContainer>
                <SubContainer capacity="1"><Containable tags="deep_muzzle_short" /></SubContainer>
              </ItemContainer>
            </Item>
            """);
        XElement container = root.Element("ItemContainer")!;
        if (hasStockSlot) { container.Add(new XElement("SubContainer", new XAttribute("capacity", 1))); }
        XElement magazine = new(container.Element("Containable")!);
        XElement lowerRail = new(container.Element("SubContainer")!);
        Item weapon = (Item)RuntimeHelpers.GetUninitializedObject(typeof(Item));
        ItemPrefab prefab = (ItemPrefab)RuntimeHelpers.GetUninitializedObject(typeof(ItemPrefab));
        AccessTools.Field(typeof(Prefab), nameof(Prefab.Identifier)).SetValue(prefab, "deep_hk416".ToIdentifier());
        AccessTools.Field(typeof(MapEntity), nameof(MapEntity.Prefab)).SetValue(weapon, prefab);
        GunsmithQuickSlotCapacityPatch.RegisterQuickSlotCapacity("deep_hk416", 6, "deep_accessory");

        try
        {
            Apply(container, weapon);
            Assert.True(XNode.DeepEquals(magazine, container.Element("Containable")));
            XElement[] slots = container.Elements("SubContainer").ToArray();
            Assert.Equal(6, slots.Length);
            Assert.True(XNode.DeepEquals(lowerRail, slots[0]));
            foreach (XElement rail in slots.Skip(1).Take(2))
            {
                XElement original = rail.Elements("Containable").First();
                Assert.Equal("flashlight,deep_laser", (string?)original.Attribute("items"));
                Assert.Equal("30,6", (string?)original.Attribute("itempos"));
                Assert.Equal("true", (string?)original.Attribute("setactive"));
                XElement fallback = Assert.Single(rail.Elements("Containable"), c => (string?)c.Attribute("tags") == "deep_accessory");
                RelatedItem rule = RelatedItem.Load(new ContentXElement(null, fallback), returnEmpty: false, parentDebugName: "HK416 rail");
                Assert.True(rule.SetActive);
                Assert.Contains("deep_accessory".ToIdentifier(), rule.Identifiers);
            }
            XElement once = new(container);
            Apply(container, weapon);
            Assert.True(XNode.DeepEquals(once, container));
        }
        finally
        {
            GunsmithQuickSlotCapacityPatch.Reset();
        }
    }

    [Fact]
    public void UnregisteredContainerRemainsUnchanged()
    {
        XElement container = XElement.Parse("<ItemContainer capacity='1'><SubContainer><Containable items='flashlight' /></SubContainer></ItemContainer>");
        XElement original = new(container);
        Apply(container);
        Assert.True(XNode.DeepEquals(original, container));
    }

    private static void Apply(XElement container, Item? weapon = null)
        => typeof(GunsmithQuickSlotCapacityPatch)
            .GetMethod("InjectGunsmithQuickSubContainers", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [weapon, new ContentXElement(null, container)]);

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
