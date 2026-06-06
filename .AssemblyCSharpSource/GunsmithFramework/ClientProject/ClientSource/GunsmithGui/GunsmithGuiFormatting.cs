namespace GunsmithFramework
{
    public static partial class GunsmithGui
    {
        private static string Key(string suffix)
            => $"{activeLocalizationPrefix}.{suffix}";

        private static string LocalizationPrefixFromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) { return DefaultLocalizationPrefix; }

            const string titleSuffix = ".ui.title";
            const string quickTitleSuffix = ".ui.quick_title";
            if (title.EndsWith(titleSuffix, StringComparison.Ordinal))
            {
                return title[..^titleSuffix.Length];
            }
            if (title.EndsWith(quickTitleSuffix, StringComparison.Ordinal))
            {
                return title[..^quickTitleSuffix.Length];
            }
            return DefaultLocalizationPrefix;
        }

        private static LocalizedString L(string key)
            => TextManager.Get(key).Fallback(key);

        private static string LocalizeKey(string key)
            => L(key).Value;

        private static LocalizedString FormatL(string key, params object?[] args)
            => (LocalizedString)FormatLValue(key, args);

        private static string FormatLValue(string key, params object?[] args)
        {
            string value = LocalizeKey(key);
            for (int i = 0; i < args.Length; i++)
            {
                value = value.Replace("{" + i + "}", args[i]?.ToString() ?? string.Empty, StringComparison.Ordinal);
            }
            return value;
        }

        private static string LocalizePathLabel(string pathLabel)
        {
            if (string.IsNullOrWhiteSpace(pathLabel)) { return LocalizeKey(Key("ui.weapon_root")); }
            return string.Join(" > ",
                pathLabel
                    .Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(LocalizeKey));
        }

        private static string LocalizedItemName(Item? item)
        {
            string identifier = item?.Prefab?.Identifier.Value ?? string.Empty;
            return string.IsNullOrWhiteSpace(identifier)
                ? string.Empty
                : TextManager.Get("entityname." + identifier).Fallback(identifier).Value;
        }

        private static string InstallButtonText(GunsmithGuiPart part, bool installed)
        {
            if (installed || part.Status == "installed") { return LocalizeKey(Key("action.installed")); }
            return part.Status switch
            {
                "missing" => LocalizeKey(Key("action.missing")),
                "incompatible" => LocalizeKey(Key("action.incompatible")),
                "disabled" => LocalizeKey(Key("action.disabled")),
                _ => LocalizeKey(part.Id == EmptyPartId ? Key("action.remove") : Key("action.install"))
            };
        }

        private readonly record struct GunsmithStatDisplay(string Text, float Value);

        private static List<GunsmithStatDisplay> FormatStats(GunsmithStats stats)
        {
            List<GunsmithStatDisplay> entries = new();

            AddNonZeroStatLine(entries, Key("stat.ergonomics"), stats.Ergonomics, "0.##");
            foreach (KeyValuePair<StatTypes, float> stat in stats.Values.OrderBy(stat => stat.Key))
            {
                AddNonZeroStatTypeLine(entries, stat.Key, stat.Value);
            }
            if (entries.Count == 0)
            {
                entries.Add(new GunsmithStatDisplay(LocalizeKey(Key("stat.none")), 0.0f));
            }
            return entries;
        }

        private static void AddNonZeroStatTypeLine(List<GunsmithStatDisplay> entries, StatTypes statType, float value)
        {
            if (Math.Abs(value) < 0.0001f) { return; }
            if (IsFlatStat(statType))
            {
                entries.Add(new GunsmithStatDisplay($"{LocalizeStatType(statType)}: {value:+0.#;-0.#;0}", value));
            }
            else
            {
                entries.Add(new GunsmithStatDisplay($"{LocalizeStatType(statType)}: {value * 100:+0.#;-0.#;0}%", value));
            }
        }

        private static void AddNonZeroStatLine(List<GunsmithStatDisplay> entries, string key, float value, string format)
        {
            if (Math.Abs(value) < 0.0001f) { return; }
            entries.Add(new GunsmithStatDisplay($"{LocalizeKey(key)}: {value.ToString("+" + format + ";-" + format + ";0", System.Globalization.CultureInfo.InvariantCulture)}", value));
        }

        private static string LocalizeStatType(StatTypes statType)
        {
            string key = Key($"stattypes.{statType}");
            return TextManager.Get(key).Fallback(statType.ToString()).Value;
        }

        private static bool IsFlatStat(StatTypes statType)
            => statType.ToString().EndsWith("SkillBonus", StringComparison.Ordinal) ||
               statType.ToString().EndsWith("SkillOverride", StringComparison.Ordinal) ||
               statType is StatTypes.ExtraLevelGain or
                   StatTypes.ExtraMissionCount or
                   StatTypes.ExtraSpecialSalesCount or
                   StatTypes.MaxAttachableCount or
                   StatTypes.LockedTalents or
                   StatTypes.InventoryExtraStackSize;

        private static Color StatDisplayColor(GunsmithStatDisplay stat)
        {
            if (stat.Value > 0.0001f) { return Color.LightGreen; }
            if (stat.Value < -0.0001f) { return Color.IndianRed; }
            return Color.LightGray;
        }
    }
}

