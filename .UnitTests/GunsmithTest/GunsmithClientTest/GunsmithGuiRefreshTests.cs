using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Barotrauma;
using GunsmithFramework;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace GunsmithClientTest;

public sealed class GunsmithGuiRefreshTests : IDisposable
{
    private static readonly List<string> Calls = [];

    public GunsmithGuiRefreshTests() => AssemblyLoadContext.Default.Resolving += ResolveGameDependency;
    public void Dispose() => AssemblyLoadContext.Default.Resolving -= ResolveGameDependency;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartChangesRefreshBeforeCollectingControlsAndCoalesce(bool atWorkbench)
    {
        using var patches = new RefreshPatches("RebuildSlotList", "RebuildPreviewPanel", "RefreshPartList", "RefreshPartDetailPanel");
        Item item = Uninitialized<Item>();
        Item? workbench = atWorkbench ? Uninitialized<Item>() : null;
        GUIFrame window = CreateGui<UpdateListProbe>();
        Set("activeWindow", window);
        Set("activeItem", item);
        Set("activeWorkbench", workbench);
        try
        {
            if (atWorkbench)
            {
                GunsmithGui.OpenAtWorkbenchFromLua(item, workbench!, "gunsmith.framework.ui.title", Spec("old"));
            }
            else
            {
                GunsmithGui.OpenFromLua(item, "gunsmith.framework.ui.title", Spec("old"));
            }
            Assert.Empty(Calls);
            GunsmithGui.RefreshPartsFromLua(item, Spec("new"));
            Assert.Empty(Calls);

            GunsmithGui.RefreshWindow();
            Assert.Equal(new[] { "RebuildSlotList", "RebuildPreviewPanel", "RefreshPartList", "RefreshPartDetailPanel", "CollectControls" }, Calls);
            Assert.Same(window, Get("activeWindow"));
            var slots = Assert.IsType<List<GunsmithGui.GunsmithGuiSlot>>(Get("activeSlots"));
            Assert.Equal("new", Assert.Single(slots).CurrentPartId);
            Assert.Equal("new", Get("selectedPartId"));
            Assert.Equal(12, Assert.IsType<GunsmithGui.GunsmithStats>(Get("activeWeaponStats")).Ergonomics);

            Calls.Clear();
            GunsmithGui.RefreshWindow();
            Assert.Equal(new[] { "CollectControls" }, Calls);

            GunsmithGui.RefreshPartsFromLua(item, Spec("old"));
            GunsmithGui.CloseWindow();
            Calls.Clear();
            GunsmithGui.RefreshWindow();
            Assert.Empty(Calls);
            Assert.Equal(false, Get("pendingPartsRefresh"));
        }
        finally
        {
            GunsmithGui.Reset();
        }
    }

    [Theory]
    [InlineData("barrel", "new", "new", "UpdatePartDetailAction")]
    [InlineData("barrel", "old", "new", "RebuildPartDetailPanel")]
    [InlineData("scope", "new", "new", "RebuildPartDetailPanel")]
    [InlineData("barrel", "removed", "removed", "RebuildPartDetailPanel")]
    public void DetailControlsAreReusedOnlyForTheSameSlotAndPart(string oldSlot, string displayedPart, string selectedPart, string expectedCall)
    {
        using var patches = new RefreshPatches("RebuildPartDetailPanel", "UpdatePartDetailAction");
        Set("partDetailPanel", Uninitialized<GUIFrame>());
        Set("partDetailActionButton", Uninitialized<GUIButton>());
        Set("partDetailSlotPath", oldSlot);
        Set("partDetailPartId", displayedPart);
        Set("selectedPartId", selectedPart);
        try
        {
            var slot = Assert.Single(GunsmithGui.ParseSpec(Spec("new")).Slots);
            AccessTools.Method(typeof(GunsmithGui), "RefreshPartDetailPanel").Invoke(null, [slot]);
            Assert.Equal(new[] { expectedCall }, Calls);
        }
        finally
        {
            GunsmithGui.Reset();
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void EscapeClosesWindowWithoutReachingBackgroundInputOrAffectingOtherKeys(bool quickMode, bool atWorkbench)
    {
        using var input = new EscapeInputPatches();
        GUIFrame window = CreateGui<UpdateListProbe>();
        window.Visible = true;
        Set("activeWindow", window);
        Set("activeItem", Uninitialized<Item>());
        Set("activeWorkbench", atWorkbench ? Uninitialized<Item>() : null);
        Set("activeQuickMode", quickMode);

        PlayerInput.Update(0.016);

        Assert.Null(GunsmithGui.ActiveWindowForInputBlock);
        Assert.Null(Get("activeItem"));
        Assert.Null(Get("activeWorkbench"));
        Assert.False(PlayerInput.KeyHit(Keys.Escape));
        Assert.False(PlayerInput.KeyHit(Keys.Escape));
        Assert.True(PlayerInput.KeyHit(Keys.A));
        Assert.False(PlayerInput.KeyHit(Keys.LeftShift));
        Assert.True(PlayerInput.KeyDown(Keys.LeftShift));

        PlayerInput.Update(0.016);
        Assert.False(PlayerInput.KeyHit(Keys.Escape));
        EscapeInputPatches.NextKeys = new KeyboardState(Keys.LeftShift);
        PlayerInput.Update(0.016);
        EscapeInputPatches.NextKeys = new KeyboardState(Keys.Escape, Keys.LeftShift);
        PlayerInput.Update(0.016);
        Assert.True(PlayerInput.KeyHit(Keys.Escape));
    }

    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    public void EscapeLeavesInactiveOrBlockedWindowsAndOtherInputAlone(bool open, bool visible, bool paused, bool escapeHit)
    {
        using var input = new EscapeInputPatches();
        GUIFrame? window = open ? CreateGui<UpdateListProbe>() : null;
        if (window != null) { window.Visible = visible; }
        Set("activeWindow", window);
        AccessTools.PropertySetter(typeof(GUI), nameof(GUI.PauseMenuOpen)).Invoke(null, [paused]);
        EscapeInputPatches.NextKeys = escapeHit
            ? new KeyboardState(Keys.Escape, Keys.A, Keys.LeftShift)
            : new KeyboardState(Keys.A, Keys.LeftShift);

        PlayerInput.Update(0.016);

        Assert.Same(window, GunsmithGui.ActiveWindowForInputBlock);
        Assert.Equal(escapeHit, PlayerInput.KeyHit(Keys.Escape));
        Assert.True(PlayerInput.KeyHit(Keys.A));
    }

    private sealed class EscapeInputPatches : IDisposable
    {
        private static readonly FieldInfo OldKeysField = AccessTools.Field(typeof(PlayerInput), "oldKeyboardState");
        private static readonly FieldInfo KeysField = AccessTools.Field(typeof(PlayerInput), "keyboardState");
        private static readonly FieldInfo AllowInputField = AccessTools.Field(typeof(PlayerInput), "allowInput");
        private readonly Harmony harmony = new(nameof(EscapeInputPatches));
        private readonly bool wasPaused = GUI.PauseMenuOpen;
        private readonly object? oldKeys = OldKeysField.GetValue(null);
        private readonly object? keys = KeysField.GetValue(null);
        private readonly object? allowInput = AllowInputField.GetValue(null);
        public static KeyboardState NextKeys;

        public EscapeInputPatches()
        {
            GunsmithGui.Reset();
            NextKeys = new KeyboardState(Keys.Escape, Keys.A, Keys.LeftShift);
            KeysField.SetValue(null, new KeyboardState(Keys.LeftShift));
            AllowInputField.SetValue(null, true);
            harmony.Patch(AccessTools.PropertyGetter(typeof(GameMain), nameof(GameMain.WindowActive)),
                prefix: new HarmonyMethod(typeof(EscapeInputPatches), nameof(KeepWindowActive)));
            harmony.Patch(AccessTools.Method(typeof(PlayerInput), nameof(PlayerInput.Update)),
                prefix: new HarmonyMethod(typeof(EscapeInputPatches), nameof(SimulateHardwareInput)),
                postfix: new HarmonyMethod(typeof(GunsmithQuickOverlayInputPatch), "CloseGunsmithWindowOnEscape"));
        }

        private static bool SimulateHardwareInput()
        {
            OldKeysField.SetValue(null, KeysField.GetValue(null));
            KeysField.SetValue(null, NextKeys);
            return false;
        }

        private static bool KeepWindowActive(ref bool __result)
        {
            __result = true;
            return false;
        }

        public void Dispose()
        {
            GunsmithGui.Reset();
            OldKeysField.SetValue(null, oldKeys);
            KeysField.SetValue(null, keys);
            AllowInputField.SetValue(null, allowInput);
            AccessTools.PropertySetter(typeof(GUI), nameof(GUI.PauseMenuOpen)).Invoke(null, [wasPaused]);
            harmony.UnpatchSelf();
        }
    }

    private static string Spec(string installed)
        => $"|ui.root|::::Ergonomics=12::barrel|part.barrel|{installed}|1|old:part.old:available,new:part.new:available";

    private static bool TraceCall(MethodBase __originalMethod)
    {
        Calls.Add(__originalMethod.Name);
        return false;
    }

    private sealed class RefreshPatches : IDisposable
    {
        private readonly Harmony harmony = new(nameof(GunsmithGuiRefreshTests));
        public RefreshPatches(params string[] methods)
        {
            GunsmithGui.Reset();
            Calls.Clear();
            foreach (string method in methods)
            {
                harmony.Patch(AccessTools.Method(typeof(GunsmithGui), method),
                    prefix: new HarmonyMethod(typeof(GunsmithGuiRefreshTests), nameof(TraceCall)));
            }
        }
        public void Dispose() => harmony.UnpatchSelf();
    }

    private sealed class UpdateListProbe : GUIFrame
    {
        private UpdateListProbe() : base(null!) { }
        public override void AddToGUIUpdateList(bool ignoreChildren = false, int order = 0)
            => Calls.Add("CollectControls");
    }

    private static T CreateGui<T>() where T : GUIComponent
    {
        T component = Uninitialized<T>();
        component.RectTransform = new RectTransform(new Point(600, 400));
        return component;
    }

    private static T Uninitialized<T>() where T : class
        => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static object? Get(string name) => AccessTools.Field(typeof(GunsmithGui), name).GetValue(null);
    private static void Set(string name, object? value) => AccessTools.Field(typeof(GunsmithGui), name).SetValue(null, value);

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
