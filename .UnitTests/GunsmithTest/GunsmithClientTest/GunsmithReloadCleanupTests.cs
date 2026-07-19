using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Barotrauma;
using Barotrauma.Items.Components;
using GunsmithFramework;
using Microsoft.Xna.Framework;
using Xunit;

namespace GunsmithClientTest;

public sealed class GunsmithReloadCleanupTests
{
    [Fact]
    public void Reset_RestoresTrackedVisualSlotLayouts()
    {
        VisualSlot first = new(new Rectangle(10, 20, 30, 40))
        {
            InteractRect = new Rectangle(11, 21, 28, 38),
            SubInventoryDir = -1
        };
        VisualSlot second = new(new Rectangle(50, 60, 30, 40))
        {
            InteractRect = new Rectangle(51, 61, 28, 38),
            SubInventoryDir = 1
        };
        Inventory inventory = (Inventory)RuntimeHelpers.GetUninitializedObject(typeof(Inventory));
        inventory.visualSlots = new[] { first, second };

        MethodInfo capture = typeof(GunsmithHiddenQuickSlotsPatch).GetMethod(
            "CaptureOriginalLayouts",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        Type ownerPlacementType = capture.GetParameters()[1].ParameterType;
        capture.Invoke(null, new[] { inventory, Activator.CreateInstance(ownerPlacementType)! });

        first.Rect = new Rectangle(100, 200, 1, 1);
        first.InteractRect = new Rectangle(101, 201, 1, 1);
        first.SubInventoryDir = 0;
        second.Rect = first.Rect;
        second.InteractRect = first.InteractRect;
        second.SubInventoryDir = 0;

        GunsmithHiddenQuickSlotsPatch.Reset();

        Assert.Equal(new Rectangle(10, 20, 30, 40), first.Rect);
        Assert.Equal(new Rectangle(11, 21, 28, 38), first.InteractRect);
        Assert.Equal(-1, first.SubInventoryDir);
        Assert.Equal(new Rectangle(50, 60, 30, 40), second.Rect);
        Assert.Equal(new Rectangle(51, 61, 28, 38), second.InteractRect);
        Assert.Equal(1, second.SubInventoryDir);
    }

    [Fact]
    public void DisposeWithoutOpenWindowClearsGuiCachesAndIsIdempotent()
    {
        AssemblyLoadContext.Default.Resolving += ResolveGameDependency;
        try
        {
            RunClientDisposeTest();
        }
        finally
        {
            AssemblyLoadContext.Default.Resolving -= ResolveGameDependency;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunClientDisposeTest()
    {
        SetStaticField(typeof(GunsmithGui), "activeWindow", null);
        AddToSet(typeof(GunsmithGui), "warnedQuickAnchorPaths", "receiver/optic");
        ((IDictionary)GetStaticField(typeof(GunsmithGui), "partIconSourceCache")!).Add("part", new Rectangle(1, 2, 3, 4));

        IDictionary partRows = (IDictionary)GetStaticField(typeof(GunsmithGui), "partRows")!;
        Type rowType = partRows.GetType().GetGenericArguments()[1];
        partRows.Add("part", Activator.CreateInstance(
            rowType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { Uninitialized<GUIFrame>(), Uninitialized<GUITextBlock>(), Uninitialized<GUITextBlock>() },
            culture: null)!);

        GunsmithApi.Dispose();
        GunsmithApi.Dispose();

        AssertRegistryEmpty(typeof(GunsmithGui), "warnedQuickAnchorPaths");
        AssertRegistryEmpty(typeof(GunsmithGui), "partIconSourceCache");
        AssertRegistryEmpty(typeof(GunsmithGui), "partRows");
        Assert.Null(GetStaticField(typeof(GunsmithGui), "activeWindow"));
        Assert.Null(GetStaticField(typeof(GunsmithGui), "selectedSlot"));
        Assert.False((bool)GetStaticField(typeof(GunsmithGui), "handlingNativeQuickDragDrop")!);

        Type quickOverlayType = typeof(GunsmithGui).GetNestedType("QuickOverlayFrame", BindingFlags.NonPublic)!;
        Assert.Null(GetStaticField(quickOverlayType, "lineTexture"));
    }

    [Fact]
    public void FabricatorResetRestoresHandlersLayoutsAndInventoryState()
    {
        GunsmithFabricatorClientPatch.Reset();

        GUIFrame root = CreateGui<GUIFrame>(new RectTransform(new Point(600, 400)));
        Point originalCategorySize = new(48, 36);
        GUIButton categoryButton = CreateGui<GUIButton>(new RectTransform(originalCategorySize, root.RectTransform));
        GUIButton.OnClickedHandler originalHandler = (_, _) => true;
        categoryButton.OnClicked = originalHandler;

        Fabricator fabricator = Uninitialized<Fabricator>();
        Type stateType = typeof(GunsmithFabricatorClientPatch).GetNestedType("ClientState", BindingFlags.NonPublic)!;
        object state = Activator.CreateInstance(stateType, nonPublic: true)!;
        object states = GetStaticField(typeof(GunsmithFabricatorClientPatch), "States")!;
        states.GetType().GetMethod("Add")!.Invoke(states, new[] { fabricator, state });

        typeof(GunsmithFabricatorClientPatch).GetMethod("WrapOriginalCategoryButtons", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { fabricator, state, new[] { categoryButton } });
        categoryButton.RectTransform.NonScaledSize = new Point(12);

        GUIFrame inputArea = CreateGui<GUIFrame>(new RectTransform(new Point(300, 120), root.RectTransform));
        GUIFrame inputHolder = CreateGui<GUIFrame>(new RectTransform(new Vector2(0.7f, 1.0f), inputArea.RectTransform));
        Vector2 originalInputSize = inputHolder.RectTransform.RelativeSize;
        inputHolder.RectTransform.RelativeSize = new Vector2(0.55f, 1.0f);
        SetInstanceProperty(state, "InputInventoryHolder", inputHolder);
        SetInstanceProperty(state, "OriginalInputInventoryHolderSize", originalInputSize);

        GUIButton gunsmithButton = CreateGui<GUIButton>(new RectTransform(new Point(36), root.RectTransform));
        GUIFrame weaponArea = CreateGui<GUIFrame>(new RectTransform(new Vector2(0.15f, 1.0f), inputArea.RectTransform));
        GUIFrame weaponHolder = CreateGui<GUIFrame>(new RectTransform(new Vector2(0.72f, 0.9f), weaponArea.RectTransform));
        SetInstanceProperty(state, "GunsmithCategoryButton", gunsmithButton);
        SetInstanceProperty(state, "WeaponArea", weaponArea);
        SetInstanceProperty(state, "WeaponInventoryHolder", weaponHolder);

        ItemContainer weaponContainer = Uninitialized<ItemContainer>();
        FieldInfo inventoryField = typeof(ItemContainer).GetField(nameof(ItemContainer.Inventory))!;
        inventoryField.SetValue(weaponContainer, RuntimeHelpers.GetUninitializedObject(inventoryField.FieldType));
        RectTransform originalInventoryTransform = new(new Point(20), root.RectTransform);
        weaponContainer.AllowUIOverlap = false;
        weaponContainer.DrawInventory = false;
        weaponContainer.UILabel = "vanilla";
        weaponContainer.Inventory.DrawWhenEquipped = false;
        weaponContainer.Inventory.RectTransform = originalInventoryTransform;

        typeof(GunsmithFabricatorClientPatch).GetMethod("CaptureWeaponContainer", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new[] { state, weaponContainer });
        typeof(GunsmithFabricatorClientPatch).GetMethod("AttachWeaponContainerToGui", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { weaponContainer, weaponHolder });
        typeof(ItemContainer).BaseType!.GetField("drawable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(weaponContainer, true);

        GunsmithFabricatorClientPatch.Reset();
        GunsmithFabricatorClientPatch.Reset();

        Assert.Same(originalHandler, categoryButton.OnClicked);
        Assert.Equal(originalCategorySize, categoryButton.RectTransform.NonScaledSize);
        Assert.Equal(originalInputSize, inputHolder.RectTransform.RelativeSize);
        Assert.Null(gunsmithButton.RectTransform.Parent);
        Assert.Null(weaponArea.RectTransform.Parent);
        Assert.Null(weaponHolder.RectTransform.Parent);
        Assert.False(weaponContainer.AllowUIOverlap);
        Assert.False(weaponContainer.DrawInventory);
        Assert.False(weaponContainer.HideItems);
        Assert.Equal("vanilla", weaponContainer.UILabel);
        Assert.False(weaponContainer.Inventory.DrawWhenEquipped);
        Assert.Same(originalInventoryTransform, weaponContainer.Inventory.RectTransform);

        object[] lookup = { fabricator, null! };
        Assert.False((bool)states.GetType().GetMethod("TryGetValue")!.Invoke(states, lookup)!);
    }

    private static T CreateGui<T>(RectTransform rectTransform) where T : GUIComponent
    {
        T component = Uninitialized<T>();
        typeof(GUIComponent).GetProperty(nameof(GUIComponent.RectTransform))!.SetValue(component, rectTransform);
        return component;
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

    private static void SetInstanceProperty(object instance, string propertyName, object? value)
        => instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(instance, value);

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
}
