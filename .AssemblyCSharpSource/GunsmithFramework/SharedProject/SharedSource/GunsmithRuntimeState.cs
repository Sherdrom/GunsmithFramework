using System.Globalization;

namespace GunsmithFramework
{
    internal static class GunsmithRuntimeStates
    {
        private static readonly ConcurrentDictionary<Item, GunsmithRuntimeState> States = new();
        internal static bool TryGet(Item? item, out GunsmithRuntimeState state)
        {
            state = null!;
            return item != null && !item.Removed && States.TryGetValue(item, out state!);
        }

        internal static bool ApplyFromLua(Item item, string signature, string statsSpec)
        {
            if (item == null || item.Removed || string.IsNullOrWhiteSpace(signature))
            {
                return false;
            }

            Set(item, CreateState(signature, statsSpec));
            return true;
        }

        internal static GunsmithRuntimeState CreateState(string signature, string statsSpec)
            => new()
            {
                Signature = signature,
                Stats = ParseRuntimeStats(statsSpec),
                HasMissingRequiredParts = signature.Contains("|missingRequired:1|", StringComparison.Ordinal)
            };

        internal static void Set(Item item, GunsmithRuntimeState state)
        {
            States[item] = state;
            GunsmithRuntimeEffectsPatch.InvalidateItem(item);
        }

        internal static bool Remove(Item? item)
        {
            if (item == null)
            {
                return false;
            }

            if (!States.TryRemove(item, out _))
            {
                return false;
            }

            GunsmithRuntimeEffectsPatch.InvalidateItem(item);
            return true;
        }

        internal static void Clear()
        {
            States.Clear();
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

    }

    internal sealed class GunsmithRuntimeState
    {
        public string Signature { get; init; } = string.Empty;
        public GunsmithRuntimeStats Stats { get; init; } = GunsmithRuntimeStats.Empty;
        public bool HasMissingRequiredParts { get; init; }
    }

    internal sealed class GunsmithRuntimeStats
    {
        public float Ergonomics { get; init; }
        public IReadOnlyDictionary<StatTypes, float> Values { get; init; } = new Dictionary<StatTypes, float>();

        public bool TryGet(StatTypes statType, out float value)
            => Values.TryGetValue(statType, out value) && value != 0.0f;

        internal static bool IsFlatStat(StatTypes statType)
            => statType.ToString().EndsWith("SkillBonus", StringComparison.Ordinal) ||
               statType.ToString().EndsWith("SkillOverride", StringComparison.Ordinal) ||
               statType is StatTypes.ExtraLevelGain or
                   StatTypes.ExtraMissionCount or
                   StatTypes.ExtraSpecialSalesCount or
                   StatTypes.MaxAttachableCount or
                   StatTypes.LockedTalents or
                   StatTypes.InventoryExtraStackSize;

        internal static float ClampStatValue(StatTypes statType, float value)
            => IsFlatStat(statType) ? value : Math.Max(value, -1.0f);

        public static GunsmithRuntimeStats Empty { get; } = new();
    }
}
