using Barotrauma.Items.Components;

namespace GunsmithFramework
{
    internal static class GunsmithDataAccess
    {
        private static readonly string ComponentTypeName = typeof(GunsmithData).FullName!;

        internal static string GetSavedState(Item? item)
            => GetSavedState(Find(item));

        internal static string GetSavedState(ItemComponent? component)
        {
            return component?.GetType()
                       .GetProperty(nameof(GunsmithData.SavedState), BindingFlags.Instance | BindingFlags.Public)?
                       .GetValue(component) as string
                   ?? string.Empty;
        }

        internal static bool SetSavedState(Item? item, string state)
            => SetSavedState(Find(item), state);

        internal static bool SetSavedState(ItemComponent? component, string state)
        {
            PropertyInfo? property = component?.GetType()
                .GetProperty(nameof(GunsmithData.SavedState), BindingFlags.Instance | BindingFlags.Public);
            if (component == null || property == null)
            {
                return false;
            }

            property.SetValue(component, GunsmithData.NormalizeSavedState(state));
            return true;
        }

        internal static bool RequestStateFromServer(Item? item)
            => Invoke(item, nameof(GunsmithData.RequestStateFromServer));

        internal static bool SubmitStateToServer(Item? item, string state)
            => Invoke(item, nameof(GunsmithData.SubmitStateToServer), state);

        internal static bool SubmitPartChangeToServer(Item? item, string slotPath, string partId)
            => Invoke(item, nameof(GunsmithData.SubmitPartChangeToServer), slotPath, partId);

        internal static bool BroadcastState(Item? item)
            => Invoke(item, nameof(GunsmithData.BroadcastState));

        private static bool Invoke(Item? item, string methodName, params object[] args)
        {
            ItemComponent? component = Find(item);
            MethodInfo? method = component?.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            if (component == null || method == null)
            {
                return false;
            }

            method.Invoke(component, args);
            return true;
        }

        private static ItemComponent? Find(Item? item)
        {
            if (item == null)
            {
                return null;
            }

            GunsmithData? current = item.GetComponent<GunsmithData>();
            return current ?? Find(item.GetComponents<ItemComponent>());
        }

        internal static ItemComponent? Find(IEnumerable<ItemComponent> components)
            => components
                .FirstOrDefault(component => component.GetType().FullName == ComponentTypeName);
    }
}
