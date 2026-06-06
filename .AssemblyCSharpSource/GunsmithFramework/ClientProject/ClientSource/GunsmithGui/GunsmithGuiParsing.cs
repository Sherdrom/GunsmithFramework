namespace GunsmithFramework
{
    public static partial class GunsmithGui
    {
        internal static GunsmithGuiSpec ParseSpec(string slotSpec)
        {
            string[] sections = slotSpec.Split(new[] { "::" }, 2, StringSplitOptions.None);
            GunsmithGuiContext context = ParseContext(sections[0]);
            GunsmithPreviewSettings previewSettings = GunsmithPreviewSettings.Default;
            GunsmithStats weaponStats = GunsmithStats.Empty;
            string slotsText = string.Empty;
            if (sections.Length > 1)
            {
                string[] remaining = sections[1].Split(new[] { "::" }, StringSplitOptions.None);
                if (remaining.Length >= 3)
                {
                    previewSettings = ParsePreviewSettings(remaining[0]);
                    weaponStats = ParseStats(remaining[1]);
                    slotsText = remaining[2];
                }
                else if (remaining.Length == 2)
                {
                    weaponStats = ParseStats(remaining[0]);
                    slotsText = remaining[1];
                }
                else
                {
                    slotsText = sections[1];
                }
            }
            return new GunsmithGuiSpec(context, previewSettings, weaponStats, ParseSlots(slotsText).ToList());
        }

        private static GunsmithGuiContext ParseContext(string contextSpec)
        {
            string[] parts = contextSpec.Split('|', 3, StringSplitOptions.TrimEntries);
            return new GunsmithGuiContext(
                parts.Length > 0 ? parts[0] : string.Empty,
                parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : DefaultLocalizationPrefix + ".ui.weapon_root",
                parts.Length > 2 ? parts[2] : string.Empty);
        }

        private static IEnumerable<GunsmithGuiSlot> ParseSlots(string slotsText)
        {
            foreach (string entry in slotsText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = entry.Split('|', 6, StringSplitOptions.TrimEntries);
                if (parts.Length < 5 || string.IsNullOrWhiteSpace(parts[0])) { continue; }

                yield return new GunsmithGuiSlot(
                    parts[0],
                    string.IsNullOrWhiteSpace(parts[1]) ? parts[0] : parts[1],
                    parts[2],
                    parts[3] == "1",
                    ParseParts(parts[4]).ToList(),
                    parts.Length > 5 ? ParseQuickMeta(parts[5]) : GunsmithQuickSlotMeta.Empty);
            }
        }

        private static GunsmithQuickSlotMeta ParseQuickMeta(string metaSpec)
        {
            int slotIndex = -1;
            float anchorX = 0.0f;
            float anchorY = 0.0f;
            bool anchorValid = false;
            HashSet<string> allowedItems = new(StringComparer.OrdinalIgnoreCase);

            foreach (string entry in metaSpec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2) { continue; }
                switch (parts[0])
                {
                    case "slot":
                        int.TryParse(parts[1], out slotIndex);
                        break;
                    case "anchorX":
                        float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out anchorX);
                        break;
                    case "anchorY":
                        float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out anchorY);
                        break;
                    case "anchorValid":
                        anchorValid = parts[1] == "1";
                        break;
                    case "items":
                        foreach (string identifier in parts[1].Split('~', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            allowedItems.Add(identifier);
                        }
                        break;
                }
            }

            return new GunsmithQuickSlotMeta(slotIndex, new Vector2(anchorX, anchorY), anchorValid, allowedItems);
        }

        private static IEnumerable<GunsmithGuiPart> ParseParts(string partSpec)
        {
            foreach (string entry in partSpec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = entry.Split(':', 7, StringSplitOptions.TrimEntries);
                if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0])) { continue; }
                yield return new GunsmithGuiPart(
                    parts[0],
                    string.IsNullOrWhiteSpace(parts[1]) ? parts[0] : parts[1],
                    parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2] : "available",
                    parts.Length > 3 ? ParseStats(parts[3]) : GunsmithStats.Empty,
                    parts.Length > 4 ? DecodeText(parts[4]) : string.Empty,
                    parts.Length > 5 ? DecodeText(parts[5]) : string.Empty,
                    parts.Length > 6 && GunsmithApi.TryParseRectangle(DecodeText(parts[6]), out Rectangle visualSourceRect) ? visualSourceRect : Rectangle.Empty);
            }
        }

        private static string DecodeText(string value)
        {
            return value
                .Replace("%3D", "=", StringComparison.OrdinalIgnoreCase)
                .Replace("%7E", "~", StringComparison.OrdinalIgnoreCase)
                .Replace("%3B", ";", StringComparison.OrdinalIgnoreCase)
                .Replace("%2C", ",", StringComparison.OrdinalIgnoreCase)
                .Replace("%7C", "|", StringComparison.OrdinalIgnoreCase)
                .Replace("%3A", ":", StringComparison.OrdinalIgnoreCase)
                .Replace("%25", "%", StringComparison.OrdinalIgnoreCase);
        }

        private static GunsmithStats ParseStats(string statSpec)
        {
            float ergonomics = 0.0f;
            Dictionary<StatTypes, float> values = new();
            foreach (string entry in statSpec.Split(new[] { ',', '~' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value)) { continue; }
                if (string.Equals(parts[0], "Ergonomics", StringComparison.Ordinal))
                {
                    ergonomics = value;
                    continue;
                }

                if (Enum.TryParse(parts[0], ignoreCase: false, out StatTypes statType) && statType != StatTypes.None)
                {
                    values[statType] = value;
                }
            }
            return new GunsmithStats { Ergonomics = ergonomics, Values = values };
        }

        private static GunsmithPreviewSettings ParsePreviewSettings(string previewSpec)
        {
            GunsmithPreviewSettings settings = GunsmithPreviewSettings.Default;
            foreach (string entry in previewSpec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value)) { continue; }
                settings = parts[0] switch
                {
                    "padding" when value >= 0 => settings with { Padding = value },
                    "scale" when value > 0 => settings with { Scale = value },
                    "offsetX" => settings with { Offset = new Vector2(value, settings.Offset.Y) },
                    "offsetY" => settings with { Offset = new Vector2(settings.Offset.X, value) },
                    _ => settings
                };
            }
            return settings;
        }
    }
}
