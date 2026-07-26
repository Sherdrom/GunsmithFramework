namespace GunsmithFramework
{
    public partial class GunsmithFramework : IAssemblyPlugin
    {
        private ISettingControl? openKey;
        private ISettingControl? quickOpenKey;

        partial void InitializePlatform()
        {
            if (Package != null)
            {
                ConfigService.TryGetConfig(Package, "OpenKey", out openKey);
                ConfigService.TryGetConfig(Package, "QuickOpenKey", out quickOpenKey);
            }
        }

        partial void OnLoadCompletedPlatform()
        {
            GunsmithApi.Initialize(GameMain.GraphicsDeviceManager.GraphicsDevice);
            if (LuaCsSetup.Instance?.Hook is Barotrauma.LuaCs.Compatibility.ILuaCsHook hook)
            {
                GunsmithApi.RegisterLuaHooks(hook);
                GunsmithLuaHooks.Add(hook, "GunsmithFrameworkOpenKeyHit", _ => openKey?.IsHit() == true);
                GunsmithLuaHooks.Add(hook, "GunsmithFrameworkQuickOpenKeyHit", _ => quickOpenKey?.IsHit() == true);
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
