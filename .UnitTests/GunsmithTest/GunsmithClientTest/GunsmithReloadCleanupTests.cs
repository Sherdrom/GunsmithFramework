using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Barotrauma;
using Barotrauma.Items.Components;
using GunsmithFramework;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace GunsmithClientTest;

public sealed class GunsmithReloadCleanupTests
{
    [Fact]
    public void PackedHiddenSlotsShareVisibleBoundsAndResetRestoresLayouts()
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

        first.Rect = new Rectangle(30, 20, 30, 40);
        first.InteractRect = new Rectangle(31, 21, 28, 38);
        second.Rect = first.Rect;
        second.InteractRect = first.InteractRect;
        second.SubInventoryDir = first.SubInventoryDir;
        object layoutCaches = GetStaticField(typeof(GunsmithHiddenQuickSlotsPatch), "OriginalLayoutsByInventory")!;
        object[] cacheLookup = [inventory, null!];
        Assert.True((bool)layoutCaches.GetType().GetMethod("TryGetValue")!.Invoke(layoutCaches, cacheLookup)!);
        MethodInfo isPacked = typeof(GunsmithHiddenQuickSlotsPatch).GetMethod(
            "IsPackedLayoutCurrent",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.True((bool)isPacked.Invoke(null, [cacheLookup[1], new[] { false, true }])!);

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

    [Theory]
    [InlineData(100, 20, 120, 30)]
    [InlineData(100, 20, 70, 55)]
    public void HiddenQuickSlotsCenterOneOrTwoVisibleSlots(int targetCenterX, int left, int right, int expectedOffset)
        => Assert.Equal(expectedOffset, GunsmithHiddenQuickSlotsPatch.GetCenteredOffsetX(targetCenterX, left, right));

    [Fact]
    public void EquippedTwoHandOwnerUsesItsNonHandQuickbarSlot()
    {
        InvSlotType[] slotTypes =
        [
            InvSlotType.RightHand,
            InvSlotType.LeftHand,
            InvSlotType.Any,
            InvSlotType.Any,
            InvSlotType.Any
        ];

        Assert.Equal(4, GunsmithHiddenQuickSlotsPatch.SelectOwnerSlotIndex([0, 1, 4], slotTypes, slotTypes.Length));
        Assert.Equal(-1, GunsmithHiddenQuickSlotsPatch.SelectOwnerSlotIndex([0, 1], slotTypes, slotTypes.Length));
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
        SetStaticField(typeof(GunsmithQuickDrag), "handlingNativeQuickDragDrop", true);
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
        Assert.False((bool)GetStaticField(typeof(GunsmithQuickDrag), "handlingNativeQuickDragDrop")!);
        Assert.Null(GetStaticField(typeof(GunsmithQuickDrag), "pendingQuickDrag"));
        Assert.Null(GetStaticField(typeof(GunsmithQuickDrag), "pendingNativeQuickDragDropClearItem"));

        Type quickOverlayType = typeof(GunsmithGui).GetNestedType("QuickOverlayFrame", BindingFlags.NonPublic)!;
        Assert.Null(GetStaticField(quickOverlayType, "lineTexture"));
    }

    [Fact]
    public void FabricatorResetRestoresHandlersLayoutsAndClearsVirtualSelection()
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
        GUIButton openButton = CreateGui<GUIButton>(new RectTransform(new Point(36), weaponArea.RectTransform));
        SetInstanceProperty(state, "GunsmithCategoryButton", gunsmithButton);
        SetInstanceProperty(state, "WeaponArea", weaponArea);
        SetInstanceProperty(state, "OpenButton", openButton);
        SetInstanceProperty(state, "SelectedWeapon", Uninitialized<Item>());

        GunsmithFabricatorClientPatch.Reset();
        GunsmithFabricatorClientPatch.Reset();

        Assert.Same(originalHandler, categoryButton.OnClicked);
        Assert.Equal(originalCategorySize, categoryButton.RectTransform.NonScaledSize);
        Assert.Equal(originalInputSize, inputHolder.RectTransform.RelativeSize);
        Assert.Null(gunsmithButton.RectTransform.Parent);
        Assert.Null(weaponArea.RectTransform.Parent);
        Assert.Null(openButton.RectTransform.Parent);
        Assert.Null(stateType.GetProperty("SelectedWeapon")!.GetValue(state));

        object[] lookup = { fabricator, null! };
        Assert.False((bool)states.GetType().GetMethod("TryGetValue")!.Invoke(states, lookup)!);
    }

    [Theory]
    [InlineData("outside")]
    [InlineData("inside")]
    [InlineData("closed")]
    [InlineData("hidden")]
    [InlineData("other-category")]
    public void DraggingWorkbenchSelectionOnlyClearsReferenceOnOutsideRelease(string finish)
    {
        AssemblyLoadContext.Default.Resolving += ResolveGameDependency;
        var harmony = new Harmony(nameof(DraggingWorkbenchSelectionOnlyClearsReferenceOnOutsideRelease));
        FieldInfo controlled = AccessTools.Field(typeof(Character), "controlled");
        FieldInfo mouse = AccessTools.Field(typeof(PlayerInput), "mouseState");
        FieldInfo oldMouse = AccessTools.Field(typeof(PlayerInput), "oldMouseState");
        object previousCharacter = controlled.GetValue(null)!;
        object previousMouse = mouse.GetValue(null)!;
        object previousOldMouse = oldMouse.GetValue(null)!;
        GUIComponent previousMouseOn = GUI.MouseOn;
        try
        {
            harmony.Patch(AccessTools.PropertyGetter(typeof(PlayerInput), "AllowInput"),
                prefix: new HarmonyMethod(typeof(GunsmithReloadCleanupTests), nameof(AllowSelectionTestInput)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(GUI), nameof(GUI.InputBlockingMenuOpen)),
                prefix: new HarmonyMethod(typeof(GunsmithReloadCleanupTests), nameof(NoSelectionTestMenu)));
            harmony.Patch(AccessTools.Method(typeof(GunsmithFabricatorClientPatch), "RefreshRecipeList"),
                prefix: new HarmonyMethod(typeof(GunsmithReloadCleanupTests), nameof(SkipSelectionTestRecipeUi)));
            GunsmithFabricatorClientPatch.Reset();
            Character character = Uninitialized<Character>();
            controlled.SetValue(null, character);
            Fabricator fabricator = Uninitialized<Fabricator>();
            Item workbench = Uninitialized<Item>();
            AccessTools.Field(typeof(Item), "activeHUDs").SetValue(workbench, new List<ItemComponent> { fabricator });
            AccessTools.Field(typeof(Character), "_selectedItem").SetValue(character, workbench);
            GUIFrame frame = CreateGui<GUIFrame>(new RectTransform(new Point(600, 400), new RectTransform(new Point(800, 600))));
            frame.Visible = true;
            GUICustomComponent slot = CreateGui<GUICustomComponent>(new RectTransform(new Point(60, 60), frame.RectTransform));
            slot.Visible = true;
            fabricator.GuiFrame = frame;
            object states = GetStaticField(typeof(GunsmithFabricatorClientPatch), "States")!;
            object state = states.GetType().GetMethod("GetOrCreateValue")!.Invoke(states, [fabricator])!;
            Item weapon = Uninitialized<Item>();
            Inventory source = Uninitialized<ItemInventory>();
            AccessTools.Field(typeof(Item), "parentInventory").SetValue(weapon, source);
            SetInstanceProperty(state, "SelectedWeapon", weapon);
            SetInstanceProperty(state, "WeaponSlot", slot);
            SetInstanceProperty(state, "GunsmithCategorySelected", true);
            GUI.ForceMouseOn(slot);
            Inventory.DraggingItems.Clear();
            oldMouse.SetValue(null, new MouseState());
            mouse.SetValue(null, SelectionTestMouse(slot.Rect.Center, ButtonState.Pressed));

            Assert.True(GunsmithFabricatorClientPatch.TryHandleVirtualWeaponDrop());
            Assert.True((bool)state.GetType().GetProperty("DraggingWeaponSelection")!.GetValue(state)!);
            Assert.Empty(Inventory.DraggingItems);

            if (finish == "closed") { AccessTools.Field(typeof(Character), "_selectedItem").SetValue(character, null); }
            if (finish == "hidden") { frame.Visible = false; }
            if (finish == "other-category") { SetInstanceProperty(state, "GunsmithCategorySelected", false); }
            oldMouse.SetValue(null, mouse.GetValue(null));
            mouse.SetValue(null, SelectionTestMouse(
                finish == "inside" ? slot.Rect.Center : new Point(slot.Rect.Right + 20, slot.Rect.Bottom + 20), ButtonState.Released));
            GunsmithFabricatorClientPatch.TryHandleVirtualWeaponDrop();

            Assert.Same(finish == "outside" ? null : weapon, state.GetType().GetProperty("SelectedWeapon")!.GetValue(state));
            Assert.False((bool)state.GetType().GetProperty("DraggingWeaponSelection")!.GetValue(state)!);
            Assert.Same(source, weapon.ParentInventory);
            Assert.Empty(Inventory.DraggingItems);
        }
        finally
        {
            GunsmithFabricatorClientPatch.Reset();
            harmony.UnpatchSelf();
            controlled.SetValue(null, previousCharacter);
            mouse.SetValue(null, previousMouse);
            oldMouse.SetValue(null, previousOldMouse);
            GUI.ForceMouseOn(previousMouseOn);
            AssemblyLoadContext.Default.Resolving -= ResolveGameDependency;
        }
    }

    private static MouseState SelectionTestMouse(Point point, ButtonState left)
        => new(point.X, point.Y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
    private static bool AllowSelectionTestInput(ref bool __result) { __result = true; return false; }
    private static bool NoSelectionTestMenu(ref bool __result) { __result = false; return false; }
    private static bool SkipSelectionTestRecipeUi() => false;

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
