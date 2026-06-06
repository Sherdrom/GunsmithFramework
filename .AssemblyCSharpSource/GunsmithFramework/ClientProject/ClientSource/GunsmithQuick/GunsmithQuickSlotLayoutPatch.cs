namespace GunsmithFramework
{
    [HarmonyPatch]
    public static class GunsmithQuickSlotLayoutPatch
    {
        private static readonly ConcurrentDictionary<Item, Dictionary<int, QuickSlotLayoutRule>> RulesByItem = new();

        public static void ClearLayouts(Item item)
        {
            if (item == null) { return; }
            RulesByItem.TryRemove(item, out _);
        }

        public static void RegisterLayout(Item item, int slotIndex, Vector2 canvasAnchor, Vector2 itemPosOffset, float rotation)
        {
            if (item == null || item.Removed || slotIndex < 0)
            {
                return;
            }

            Dictionary<int, QuickSlotLayoutRule> rules = RulesByItem.GetOrAdd(item, _ => new Dictionary<int, QuickSlotLayoutRule>());
            lock (rules)
            {
                rules[slotIndex] = new QuickSlotLayoutRule(canvasAnchor, itemPosOffset, rotation);
            }
        }

        internal static bool TryGetLayoutRule(Item item, int slotIndex, out QuickSlotLayoutRule rule)
        {
            rule = default;
            if (item == null || item.Removed || slotIndex < 0)
            {
                return false;
            }

            if (!RulesByItem.TryGetValue(item, out Dictionary<int, QuickSlotLayoutRule>? rules))
            {
                return false;
            }

            lock (rules)
            {
                return rules.TryGetValue(slotIndex, out rule);
            }
        }

        internal readonly record struct QuickSlotLayoutRule(Vector2 CanvasAnchor, Vector2 ItemPosOffset, float RotationDegrees);
    }
}
