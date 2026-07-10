using System.Globalization;
using System.Threading;

namespace GunsmithFramework
{
    internal static class GunsmithRuntimeStates
    {
        private static readonly ConcurrentDictionary<Item, GunsmithRuntimeState> States = new();
        private static int managedRuntimeItemCount;

        internal static bool HasManagedRuntimeItems => Volatile.Read(ref managedRuntimeItemCount) > 0;

        internal static bool TryGet(Item? item, out GunsmithRuntimeState state)
        {
            state = null!;
            return item != null && !item.Removed && States.TryGetValue(item, out state!);
        }

        internal static bool ApplyFromLua(Item item, string signature, string statsSpec, string managedItemSpec)
        {
            if (item == null || item.Removed || string.IsNullOrWhiteSpace(signature))
            {
                return false;
            }

            Set(item, CreateState(signature, statsSpec, managedItemSpec));
            return true;
        }

        internal static GunsmithRuntimeState CreateState(string signature, string statsSpec, string managedItemSpec)
            => new()
            {
                Signature = signature,
                Stats = ParseRuntimeStats(statsSpec),
                ManagedItemIdentifiers = ParseIdentifierSet(managedItemSpec)
            };

        internal static void Set(Item item, GunsmithRuntimeState state)
        {
            bool hadManagedItems = States.TryGetValue(item, out GunsmithRuntimeState? existingState) &&
                                   existingState.ManagedItemIdentifiers.Count > 0;
            bool hasManagedItems = state.ManagedItemIdentifiers.Count > 0;

            States[item] = state;
            GunsmithRuntimeEffectsPatch.InvalidateItem(item);
            if (hadManagedItems == hasManagedItems)
            {
                return;
            }

            if (hasManagedItems)
            {
                Interlocked.Increment(ref managedRuntimeItemCount);
            }
            else
            {
                Interlocked.Decrement(ref managedRuntimeItemCount);
            }
        }

        internal static bool Remove(Item? item)
        {
            if (item == null)
            {
                return false;
            }

            if (!States.TryRemove(item, out GunsmithRuntimeState? removedState))
            {
                return false;
            }

            if (removedState.ManagedItemIdentifiers.Count > 0)
            {
                Interlocked.Decrement(ref managedRuntimeItemCount);
            }
            GunsmithRuntimeEffectsPatch.InvalidateItem(item);
            return true;
        }

        internal static void Clear()
        {
            States.Clear();
            managedRuntimeItemCount = 0;
            GunsmithRuntimeEffectsPatch.ClearCaches();
        }

        private static GunsmithRuntimeStats ParseRuntimeStats(string value)
        {
            float ergonomics = 0.0f;
            Dictionary<StatTypes, float> values = new();
            if (string.IsNullOrWhiteSpace(value)) { return GunsmithRuntimeStats.Empty; }

            foreach (string entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2 ||
                    !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                {
                    continue;
                }

                if (string.Equals(parts[0], "Ergonomics", StringComparison.Ordinal))
                {
                    ergonomics = parsed;
                    continue;
                }

                if (parsed != 0.0f &&
                    Enum.TryParse(parts[0], ignoreCase: false, out StatTypes statType) &&
                    statType != StatTypes.None)
                {
                    values[statType] = parsed;
                }
            }

            return new GunsmithRuntimeStats { Ergonomics = ergonomics, Values = values };
        }

        private static IReadOnlySet<string> ParseIdentifierSet(string value)
        {
            HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value)) { return identifiers; }

            foreach (string identifier in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(identifier))
                {
                    identifiers.Add(identifier);
                }
            }

            return identifiers;
        }
    }

    internal sealed class GunsmithRuntimeState
    {
        public string Signature { get; init; } = string.Empty;
        public GunsmithRuntimeStats Stats { get; init; } = GunsmithRuntimeStats.Empty;
        public IReadOnlySet<string> ManagedItemIdentifiers { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class GunsmithRuntimeStats
    {
        public float Ergonomics { get; init; }
        public IReadOnlyDictionary<StatTypes, float> Values { get; init; } = new Dictionary<StatTypes, float>();

        public bool TryGet(StatTypes statType, out float value)
            => Values.TryGetValue(statType, out value) && value != 0.0f;

        public static GunsmithRuntimeStats Empty { get; } = new();
    }
}
