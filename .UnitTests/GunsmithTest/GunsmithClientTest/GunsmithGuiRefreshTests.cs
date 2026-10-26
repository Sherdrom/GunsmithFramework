using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Barotrauma;
using GunsmithFramework;
using HarmonyLib;
using Microsoft.Xna.Framework;
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
