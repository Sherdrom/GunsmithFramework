namespace GunsmithFramework
{
    internal static class GunsmithLuaHooks
    {
        internal static void Call(string hookName, params object[] args)
        {
            try
            {
                if (LuaCsSetup.Instance?.Hook is Barotrauma.LuaCs.Compatibility.ILuaCsHook hook)
                {
                    hook.Call(hookName, args);
                }
            }
            catch (Exception ex)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to call Lua hook '{hookName}': {ex.Message}");
            }
        }
    }
}
