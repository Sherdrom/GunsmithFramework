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
        private static void AppendItemStats(Item item, IEnumerable<Item>? itemsInSlot, Character? character, ref RichString __result)
        {
            if (item == null || __result == null)
            {
                return;
            }

            string? stats = GetFormattedItemStats(item);
            if (stats == null)
            {
                return;
            }

            string tooltip = __result.NestedStr.Value ?? string.Empty;
            string? stackLine = itemsInSlot != null && itemsInSlot.Skip(1).Any()
                ? $"\n‖color:gui.blue‖[{GameSettings.CurrentConfig.KeyMap.KeyBindText(InputType.TakeOneFromInventorySlot)}] {TextManager.Get("inputtype.takeonefrominventoryslot")}‖color:end‖"
                : null;
            string? skillHints = item.Prefab?.SkillRequirementHints != null && item.Prefab.SkillRequirementHints.Any()
                ? item.Prefab.GetSkillRequirementHints(character).Value
                : null;
            string contextualLine = PlayerInput.KeyDown(InputType.ContextualCommand)
                ? $"\n‖color:gui.blue‖{TextManager.ParseInputTypes(TextManager.Get("itemmsgcontextualorders"))}‖color:end‖"
                : $"\n‖color:{XMLExtensions.ToStringHex(Color.LightGray * 0.7f)}‖{TextManager.Get("itemmsg.morreoptionsavailable")}‖color:end‖";
            __result = RichString.Rich(InsertTooltipStats(tooltip, stats, stackLine, skillHints, contextualLine));
        }

        internal static string? GetFormattedItemStats(object itemOrIdentifier, string indent = "  ")
        {
            if (GunsmithLuaHooks.Call("GunsmithFrameworkGetItemStats", itemOrIdentifier) is not string spec)
            {
                return null;
            }

            string[] sections = spec.Split(new[] { "::" }, 2, StringSplitOptions.None);
            if (sections.Length != 2 || string.IsNullOrWhiteSpace(sections[0]))
            {
                return null;
            }

            return FormatTooltipStats(ParseStats(sections[1]), sections[0], indent);
        }

        internal static string InsertTooltipStats(string tooltip, string stats, params string?[] beforeSections)
        {
            int insertAt = -1;
            foreach (string? section in beforeSections)
            {
                if (string.IsNullOrEmpty(section))
                {
                    continue;
                }

                int index = tooltip.LastIndexOf(section, StringComparison.Ordinal);
                if (index >= 0 && (insertAt < 0 || index < insertAt))
                {
                    insertAt = index;
                }
            }

            return insertAt < 0
                ? tooltip + "\n" + stats
                : tooltip.Insert(insertAt, "\n" + stats);
        }

        private static string FormatTooltipStats(GunsmithStats stats, string localizationPrefix, string indent)
        {
            List<string> entries = new();
            float ergonomics = GunsmithErgonomicsAimPatch.ClampErgonomics(stats.Ergonomics);
            if (Math.Abs(ergonomics) >= 0.0001f)
            {
                string name = LocalizeTooltipStat(localizationPrefix, "stat.ergonomics", "Ergonomics");
                string value = ergonomics.ToString("+0.##;-0.##;0", System.Globalization.CultureInfo.InvariantCulture);
                entries.Add(FormatTooltipStat(value, name, ergonomics, indent));
            }

            foreach (KeyValuePair<StatTypes, float> stat in stats.Values.OrderBy(stat => stat.Key))
            {
                float statValue = GunsmithRuntimeStats.ClampStatValue(stat.Key, stat.Value);
                if (Math.Abs(statValue) < 0.0001f) { continue; }
                string name = LocalizeTooltipStat(localizationPrefix, $"stattypes.{stat.Key}", stat.Key.ToString());
                string value = GunsmithRuntimeStats.IsFlatStat(stat.Key)
                    ? $"{statValue:+0.#;-0.#;0}"
                    : $"{statValue * 100:+0.#;-0.#;0}%";
                entries.Add(FormatTooltipStat(value, name, statValue, indent));
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

        internal static string FormatTooltipStat(string value, string name, float sign, string indent = "  ")
        {
            string color = XMLExtensions.ToStringHex(sign > 0.0f ? GUIStyle.Green : GUIStyle.Red);
            return $"{indent}‖color:{color}‖{value}‖color:end‖ {name}";
        }
    }
}
