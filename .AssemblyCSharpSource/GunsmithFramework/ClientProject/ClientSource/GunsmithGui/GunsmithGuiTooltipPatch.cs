namespace GunsmithFramework
{
    [HarmonyPatch]
    public static partial class GunsmithGui
    {
        [HarmonyPatch(typeof(Inventory.SlotReference), "GetTooltip", new[]
        {
            typeof(Item),
            typeof(IEnumerable<Item>),
            typeof(Character)
        })]
        [HarmonyPostfix]
        private static void AppendItemStats(Item item, ref RichString __result)
        {
            if (GunsmithLuaHooks.Call("GunsmithFrameworkGetItemStats", item) is not string spec)
            {
                return;
            }

            string[] sections = spec.Split(new[] { "::" }, 2, StringSplitOptions.None);
            if (sections.Length != 2 || string.IsNullOrWhiteSpace(sections[0]))
            {
                return;
            }

            string stats = FormatTooltipStats(ParseStats(sections[1]), sections[0]);
            string tooltip = __result?.NestedStr.Value ?? string.Empty;
            string? modLine = item.Prefab.ContentPackage is { } package && package != GameMain.VanillaContent
                ? $"\n‖color:{XMLExtensions.ToStringHex(Color.MediumPurple)}‖{package.Name}‖color:end‖"
                : null;
            __result = RichString.Rich(InsertTooltipStats(tooltip, stats, modLine));
        }

        internal static string InsertTooltipStats(string tooltip, string stats, string? modLine)
        {
            int insertAt = string.IsNullOrEmpty(modLine)
                ? -1
                : tooltip.IndexOf(modLine, StringComparison.Ordinal);
            return insertAt < 0
                ? tooltip + "\n" + stats
                : tooltip.Insert(insertAt, "\n" + stats);
        }

        private static string FormatTooltipStats(GunsmithStats stats, string localizationPrefix)
        {
            List<string> entries = new();
            float ergonomics = GunsmithErgonomicsAimPatch.ClampErgonomics(stats.Ergonomics);
            if (Math.Abs(ergonomics) >= 0.0001f)
            {
                string name = LocalizeTooltipStat(localizationPrefix, "stat.ergonomics", "Ergonomics");
                string value = ergonomics.ToString("+0.##;-0.##;0", System.Globalization.CultureInfo.InvariantCulture);
                entries.Add(FormatTooltipStat(value, name, ergonomics));
            }

            foreach (KeyValuePair<StatTypes, float> stat in stats.Values.OrderBy(stat => stat.Key))
            {
                if (Math.Abs(stat.Value) < 0.0001f) { continue; }
                string name = LocalizeTooltipStat(localizationPrefix, $"stattypes.{stat.Key}", stat.Key.ToString());
                string value = IsFlatStat(stat.Key)
                    ? $"{stat.Value:+0.#;-0.#;0}"
                    : $"{stat.Value * 100:+0.#;-0.#;0}%";
                entries.Add(FormatTooltipStat(value, name, stat.Value));
            }

            if (entries.Count == 0)
            {
                entries.Add(LocalizeTooltipStat(localizationPrefix, "stat.none", "No stat changes"));
            }

            return string.Join("\n", entries);
        }

        private static string LocalizeTooltipStat(string localizationPrefix, string suffix, string fallback)
            => TextManager.Get($"{localizationPrefix}.{suffix}")
                .Fallback(TextManager.Get($"{DefaultLocalizationPrefix}.{suffix}"), useDefaultLanguageIfFound: false)
                .Fallback(fallback).Value;

        internal static string FormatTooltipStat(string value, string name, float sign)
        {
            string color = XMLExtensions.ToStringHex(sign > 0.0f ? GUIStyle.Green : GUIStyle.Red);
            return $"  ‖color:{color}‖{value}‖color:end‖ {name}";
        }
    }
}
