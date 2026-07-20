namespace GunsmithFramework
{
    internal static class GunsmithLuaHooks
    {
        private static readonly HashSet<string> registeredNames = new(StringComparer.Ordinal);
        private static object? registeredOwner;
        private static Action<string>? unregister;

        internal static bool HasRegisteredHooks => registeredOwner != null;

        internal static void Add(
            Barotrauma.LuaCs.Compatibility.ILuaCsHook hook,
            string hookName,
            Barotrauma.LuaCsFunc callback)
        {
            // LuaCs unloads plugins before resetting its hook service, so callbacks need stable removable IDs.
            hook.Add(hookName, hookName, callback);
            Track(hook, hookName, name => hook.Remove(name, name));
        }

        internal static void Track(object owner, string hookName, Action<string> remove)
        {
            if (!ReferenceEquals(registeredOwner, owner))
            {
                Clear();
                registeredOwner = owner;
                unregister = remove;
            }

            registeredNames.Add(hookName);
        }

        internal static void Clear()
        {
            if (unregister != null)
            {
                foreach (string hookName in registeredNames)
                {
                    unregister(hookName);
                }
            }

            registeredNames.Clear();
            registeredOwner = null;
            unregister = null;
        }

        internal static object? Call(string hookName, params object[] args)
        {
            try
            {
                if (LuaCsSetup.Instance?.Hook is Barotrauma.LuaCs.Compatibility.ILuaCsHook hook)
                {
                    return hook.Call(hookName, args);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to call Lua hook '{hookName}': {ex.Message}");
                }
                catch
                {
                    // LuaCs logging can be unavailable for the same reason as the hook.
                }
            }

            return null;
        }
    }
}
