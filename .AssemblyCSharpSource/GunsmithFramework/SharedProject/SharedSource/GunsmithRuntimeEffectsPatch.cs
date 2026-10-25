namespace GunsmithFramework
{
    [HarmonyPatch]
    public static class GunsmithRuntimeEffectsPatch
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Character, HeldGunsmithStateCacheBox> HeldStateCacheByCharacter = new();

        [HarmonyPatch(typeof(Character), nameof(Character.GetStatValue), new[] { typeof(StatTypes), typeof(bool) })]
        [HarmonyPostfix]
        private static void ApplyGunsmithCharacterStats(Character __instance, StatTypes statType, ref float __result)
        {
            if (!TryGetHeldGunsmithState(__instance, out GunsmithRuntimeState? state) ||
                !state.Stats.TryGet(statType, out float value))
            {
                return;
            }

            __result = GunsmithRuntimeStats.ClampStatValue(statType, __result + value);
        }

        internal static void InvalidateItem(Item item)
            => HeldStateCacheByCharacter.Clear();

        internal static void ClearCaches()
            => HeldStateCacheByCharacter.Clear();

        private static bool TryGetHeldGunsmithState(Character character, out GunsmithRuntimeState state)
        {
            state = null!;
            if (character == null || character.Removed || character.HeldItems == null)
            {
                return false;
            }

            double cacheTime = Timing.TotalTime;
            if (HeldStateCacheByCharacter.TryGetValue(character, out HeldGunsmithStateCacheBox? cache) &&
                cache.Value.TotalTime == cacheTime)
            {
                state = cache.Value.State!;
                return cache.Value.Found;
            }

            foreach (Item item in character.HeldItems)
            {
                if (GunsmithRuntimeStates.TryGet(item, out state))
                {
                    SetHeldStateCache(character, new HeldGunsmithStateCache(cacheTime, true, state));
                    return true;
                }
            }

            SetHeldStateCache(character, new HeldGunsmithStateCache(cacheTime, false, null));
            return false;
        }

        private static void SetHeldStateCache(Character character, HeldGunsmithStateCache value)
        {
            if (HeldStateCacheByCharacter.TryGetValue(character, out HeldGunsmithStateCacheBox? cache))
            {
                cache.Value = value;
                return;
            }

            HeldStateCacheByCharacter.Add(character, new HeldGunsmithStateCacheBox(value));
        }

        private sealed class HeldGunsmithStateCacheBox
        {
            public HeldGunsmithStateCacheBox(HeldGunsmithStateCache value)
                => Value = value;

            public HeldGunsmithStateCache Value { get; set; }
        }

        private readonly record struct HeldGunsmithStateCache(double TotalTime, bool Found, GunsmithRuntimeState? State);
    }

}
