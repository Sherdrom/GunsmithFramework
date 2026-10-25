using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Barotrauma;
using GunsmithFramework;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace GunsmithClientTest;

public sealed class GunsmithComposerRecoveryTests
{
    private static RenderTargetBinding[] targets = [];
    private static readonly List<GraphicsResource> disposed = [];
    private static bool batchActive;
    private static RenderTarget2D? created;
    private static int targetCount, failAtTarget;
    private static readonly List<RenderTarget2D> allocated = [];

    [Fact]
    public void MissingTextureRestoresRenderTargetAndAllowsNextComposition()
    {
        AssemblyLoadContext.Default.Resolving += ResolveGameDependency;
        try { RunRecoveryTest(); }
        finally { AssemblyLoadContext.Default.Resolving -= ResolveGameDependency; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunRecoveryTest()
    {
        var harmony = new Harmony(nameof(GunsmithComposerRecoveryTests));
        object? originalDevice = AccessTools.Field(typeof(GunsmithApi), "graphicsDevice").GetValue(null);
        object? originalBatch = AccessTools.Field(typeof(GunsmithApi), "spriteBatch").GetValue(null);
        try
        {
            // Replace GPU operations only; execute the real composer and missing-file path.
            Patch(typeof(RenderTarget2D).GetConstructor([typeof(GraphicsDevice), typeof(int), typeof(int), typeof(bool), typeof(SurfaceFormat), typeof(DepthFormat)])!, nameof(CreateTarget));
            Patch(AccessTools.Method(typeof(GraphicsDevice), nameof(GraphicsDevice.GetRenderTargets), Type.EmptyTypes), nameof(GetTargets));
            Patch(AccessTools.Method(typeof(GraphicsDevice), nameof(GraphicsDevice.SetRenderTarget), [typeof(RenderTarget2D)]), nameof(SetTarget));
            Patch(AccessTools.Method(typeof(GraphicsDevice), nameof(GraphicsDevice.SetRenderTargets), [typeof(RenderTargetBinding[])]), nameof(SetTargets));
            Patch(AccessTools.Method(typeof(GraphicsDevice), nameof(GraphicsDevice.Clear), [typeof(Color)]), nameof(Skip));
            Patch(AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.Begin)), nameof(Begin));
            Patch(AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.End)), nameof(End));
            Patch(AccessTools.Method(typeof(GraphicsResource), nameof(GraphicsResource.Dispose), Type.EmptyTypes), nameof(DisposeResource));
            AccessTools.Field(typeof(GunsmithApi), "graphicsDevice").SetValue(null, Uninitialized<GraphicsDevice>());
            AccessTools.Field(typeof(GunsmithApi), "spriteBatch").SetValue(null, Uninitialized<SpriteBatch>());
            RenderTargetBinding[] previous = [new RenderTargetBinding(Uninitialized<RenderTarget2D>())];
            targets = previous;
            disposed.Clear();
            allocated.Clear();
            targetCount = failAtTarget = 0;
            batchActive = false;

            MethodInfo parse = AccessTools.Method(typeof(GunsmithApi), "ParseLayers");
            MethodInfo compose = AccessTools.Method(typeof(GunsmithApi), "ComposeTexture");
            object layers = parse.Invoke(null, [$"slot|part|{Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png")}|0,0,1,1|0,0|0|1"])!;
            var error = Assert.Throws<TargetInvocationException>(() => compose.Invoke(null, [layers, 8, 8]));
            Assert.IsType<FileNotFoundException>(error.InnerException);
            Assert.Same(previous, targets);
            Assert.False(batchActive);
            Assert.Contains(created!, disposed);

            object emptyLayers = parse.Invoke(null, [""])!;
            var texture = (Texture2D)compose.Invoke(null, [emptyLayers, 8, 8])!;
            Assert.Same(previous, targets);
            Assert.False(batchActive);
            Assert.DoesNotContain(texture, disposed);
            texture.Dispose();

            Patch(AccessTools.Method(typeof(GunsmithApi), "GetTexture"), nameof(GetLayerTexture));
            Patch(AccessTools.Method(typeof(SpriteBatch), nameof(SpriteBatch.Draw), [typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(float), typeof(SpriteEffects), typeof(float)]), nameof(Skip));
            Patch(AccessTools.Method(typeof(LuaCsSetup), nameof(LuaCsSetup.PrintCsMessage)), nameof(Skip));
            Item item = Uninitialized<Item>();
            AccessTools.Field(typeof(MapEntity), "Prefab").SetValue(item, Uninitialized<ItemPrefab>());
            for (int failure = 2; failure <= 3; failure++)
            {
                allocated.Clear();
                disposed.Clear();
                targetCount = 0;
                failAtTarget = failure;
                Assert.False(GunsmithApi.ApplyFromLua(item, "state", "slot|part|fake.png|0,0,1,1|0,0|0|1", "", "", "", "", 8, 8));
                Assert.Equal(failure - 1, allocated.Count);
                Assert.All(allocated, target => Assert.Contains(target, disposed));
                Assert.Same(previous, targets);
                Assert.False(batchActive);
                Assert.False(GunsmithApi.HasAnySpriteState);
            }
        }
        finally
        {
            AccessTools.Field(typeof(GunsmithApi), "graphicsDevice").SetValue(null, originalDevice);
            AccessTools.Field(typeof(GunsmithApi), "spriteBatch").SetValue(null, originalBatch);
            harmony.UnpatchSelf();
        }

        void Patch(MethodBase method, string prefix)
            => harmony.Patch(method, prefix: new HarmonyMethod(typeof(GunsmithComposerRecoveryTests), prefix));
    }

    private static bool CreateTarget(RenderTarget2D __instance)
    {
        GC.SuppressFinalize(__instance);
        if (++targetCount == failAtTarget) { throw new InvalidOperationException("injected target allocation failure"); }
        created = __instance;
        allocated.Add(__instance);
        return false;
    }
    private static bool GetLayerTexture(ref Texture2D __result) { __result = Uninitialized<Texture2D>(); return false; }
    private static bool GetTargets(ref RenderTargetBinding[] __result) { __result = targets; return false; }
    private static bool SetTarget(RenderTarget2D renderTarget) { targets = [new RenderTargetBinding(renderTarget)]; return false; }
    private static bool SetTargets(RenderTargetBinding[] renderTargets) { targets = renderTargets; return false; }
    private static bool Begin() { Assert.False(batchActive); batchActive = true; return false; }
    private static bool End() { Assert.True(batchActive); batchActive = false; return false; }
    private static bool DisposeResource(GraphicsResource __instance) { disposed.Add(__instance); return false; }
    private static bool Skip() => false;
    private static Assembly? ResolveGameDependency(AssemblyLoadContext context, AssemblyName name)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, name.Name + ".dll");
            if (File.Exists(path)) { return context.LoadFromAssemblyPath(path); }
        }
        return null;
    }

    private static T Uninitialized<T>() where T : class
    {
        var value = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        GC.SuppressFinalize(value);
        return value;
    }
}
