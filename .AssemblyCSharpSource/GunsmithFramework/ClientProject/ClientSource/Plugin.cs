namespace GunsmithFramework
{
    public partial class GunsmithFramework : IAssemblyPlugin
    {
        partial void InitializePlatform()
        {
        }

        partial void OnLoadCompletedPlatform()
        {
            GunsmithApi.Initialize(GameMain.GraphicsDeviceManager.GraphicsDevice);
            if (LuaCsSetup.Instance?.Hook is Barotrauma.LuaCs.Compatibility.ILuaCsHook hook)
            {
                GunsmithApi.RegisterLuaHooks(hook);
                GunsmithPartChangeClient.Register();
            }
            else
            {
                LuaCsSetup.PrintCsMessage("[GunsmithFramework] Compatibility hook is unavailable; Lua bridge not registered.");
            }
        }

        partial void DisposePlatform()
        {
            GunsmithApi.Dispose();
        }
    }
}
