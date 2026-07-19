namespace GunsmithFramework
{
    public partial class GunsmithFramework : IAssemblyPlugin
    {
        partial void InitializePlatform()
        {
        }

        partial void OnLoadCompletedPlatform()
        {
            if (LuaCsSetup.Instance?.Hook is Barotrauma.LuaCs.Compatibility.ILuaCsHook hook)
            {
                GunsmithServerHooks.RegisterLuaHooks(hook);
                GunsmithPartChangeServer.Register();
            }
            else
            {
                LuaCsSetup.PrintCsMessage("[GunsmithFramework] Compatibility hook is unavailable; server Lua bridge not registered.");
            }
        }

        partial void DisposePlatform()
        {
        }
    }
}
