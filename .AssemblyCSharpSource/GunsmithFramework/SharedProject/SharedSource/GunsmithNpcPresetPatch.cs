namespace GunsmithFramework
{
    [HarmonyPatch]
    public static class GunsmithNpcPresetPatch
    {
        private const string PresetAttributeName = "gunsmithpreset";
        private const string IdentifierAttributeName = "identifier";
        private static readonly ConditionalWeakTable<Item, PresetName> Presets = new();
        private static readonly List<PendingPreset> PendingItemPresets = new();
        private static readonly object PresetsLock = new();
        private static readonly object PendingItemPresetsLock = new();

        internal static void Reset()
        {
            lock (PendingItemPresetsLock)
            {
                PendingItemPresets.Clear();
            }

            lock (PresetsLock)
            {
                Presets.Clear();
            }
        }

        private sealed class PresetName
        {
            public PresetName(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private sealed class PendingPreset
        {
            public PendingPreset(string itemIdentifier, string preset)
            {
                ItemIdentifier = itemIdentifier;
                Preset = preset;
            }

            public string ItemIdentifier { get; }
            public string Preset { get; }
        }

        internal static string GetPreset(Item? item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return Presets.TryGetValue(item, out PresetName? preset)
                ? preset.Value
                : string.Empty;
        }

        [HarmonyPatch(typeof(HumanPrefab), "InitializeItem", typeof(Character), typeof(ContentXElement), typeof(Submarine), typeof(HumanPrefab), typeof(WayPoint), typeof(Item), typeof(bool))]
        [HarmonyPrefix]
        private static void QueuePresetFromHumanPrefabItem(ContentXElement itemElement)
        {
            QueuePresetElement(itemElement);
        }

        [HarmonyPatch(typeof(Item), MethodType.Constructor, typeof(Rectangle), typeof(ItemPrefab), typeof(Submarine), typeof(bool), typeof(ushort))]
        [HarmonyPostfix]
        private static void CapturePresetFromRectConstructor(Item __instance)
        {
            TryApplyPendingItemPreset(__instance);
        }

        private static void QueuePresetElement(ContentXElement element)
        {
            string preset = element.GetAttributeString(PresetAttributeName, string.Empty).Trim();
            string itemIdentifier = element.GetAttributeString(IdentifierAttributeName, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(preset) || string.IsNullOrWhiteSpace(itemIdentifier))
            {
                return;
            }

            lock (PendingItemPresetsLock)
            {
                PendingItemPresets.Add(new PendingPreset(itemIdentifier, preset));
            }
        }

        private static void TryApplyPendingItemPreset(Item? item)
        {
            if (item == null || item.Removed)
            {
                return;
            }

            string itemIdentifier = item.Prefab?.Identifier.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(itemIdentifier))
            {
                return;
            }

            string preset = string.Empty;
            lock (PendingItemPresetsLock)
            {
                for (int i = 0; i < PendingItemPresets.Count; i++)
                {
                    PendingPreset pending = PendingItemPresets[i];
                    if (!string.Equals(pending.ItemIdentifier, itemIdentifier, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    preset = pending.Preset;
                    PendingItemPresets.RemoveAt(i);
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(preset))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(GetPreset(item)))
            {
                RegisterPreset(item, preset);
                NotifyPresetRegistered(item, preset);
            }
        }

        private static void RegisterPreset(Item item, string preset)
        {
            if (string.IsNullOrWhiteSpace(preset))
            {
                return;
            }

            lock (PresetsLock)
            {
                Presets.Remove(item);
                Presets.Add(item, new PresetName(preset));
            }
        }

        private static void NotifyPresetRegistered(Item item, string preset)
        {
            try
            {
                if (LuaCsSetup.Instance?.Hook is Barotrauma.LuaCs.Compatibility.ILuaCsHook hook)
                {
                    hook.Call("GunsmithFrameworkNpcPresetRegistered", item, preset);
                }
            }
            catch (Exception ex)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to notify Lua about NPC preset: {ex.Message}");
            }
        }

    }
}
