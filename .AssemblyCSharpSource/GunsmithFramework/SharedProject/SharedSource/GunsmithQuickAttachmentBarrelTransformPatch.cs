using Barotrauma;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace GunsmithFramework
{
    public static class GunsmithQuickAttachmentBarrelTransforms
    {
        public const string PrimaryKey = "primary";
        public const string LowerRailKey = "lower_rail";

        private static readonly ConcurrentDictionary<Item, ConcurrentDictionary<string, BarrelRule>> RulesByItem = new();
        private static readonly ConcurrentDictionary<Item, string> ActiveRuleKeyByItem = new();
        private static readonly ConcurrentDictionary<Item, int> ActiveProjectileSelectionByItem = new();
        private static readonly ConcurrentDictionary<Item, CachedLocalPosition> CachedLocalPositions = new();
        private static readonly ConcurrentDictionary<Item, string> ReportedMissingRuleSignatureByItem = new();

        internal static void ClearAllTransforms()
        {
            RulesByItem.Clear();
            ActiveRuleKeyByItem.Clear();
            ActiveProjectileSelectionByItem.Clear();
            CachedLocalPositions.Clear();
            ReportedMissingRuleSignatureByItem.Clear();
        }

        public static void ClearTransforms(Item item)
        {
            if (item == null) { return; }
            RulesByItem.TryRemove(item, out _);
            ActiveRuleKeyByItem.TryRemove(item, out _);
            ActiveProjectileSelectionByItem.TryRemove(item, out _);
            CachedLocalPositions.TryRemove(item, out _);
            ReportedMissingRuleSignatureByItem.TryRemove(item, out _);
        }

        public static void RegisterTransform(Item item, string key, float localX, float localY, float rotationDegrees)
        {
            if (item == null || item.Removed)
            {
                return;
            }

            string normalizedKey = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                DebugConsole.ThrowError($"GunsmithFramework QAT received a barrel transform with an empty key. weapon={item.Prefab.Identifier.Value}");
                return;
            }

            Vector2 localPosition = new(localX, localY);
            if (!IsFinite(localPosition) || !float.IsFinite(rotationDegrees))
            {
                DebugConsole.ThrowError(
                    $"GunsmithFramework QAT received an invalid barrel transform. " +
                    $"weapon={item.Prefab.Identifier.Value}, " +
                    $"key={normalizedKey}, " +
                    $"localPosition={localPosition}, rotationDegrees={rotationDegrees}");
                return;
            }

            ConcurrentDictionary<string, BarrelRule> rules = RulesByItem.GetOrAdd(
                item,
                _ => new ConcurrentDictionary<string, BarrelRule>(StringComparer.OrdinalIgnoreCase));
            rules[normalizedKey] = new BarrelRule(localPosition, rotationDegrees);
            CachedLocalPositions.TryRemove(item, out _);
            ApplyCurrentBarrelPos(item, reportMissingActiveRule: false);
        }

        public static void ApplySelectedProjectile(Item item, int selectedProjectile)
        {
            if (item == null || item.Removed)
            {
                return;
            }

            CachedLocalPositions.TryRemove(item, out _);
            ActiveRuleKeyByItem[item] = KeyForProjectileSelection(selectedProjectile);
            ActiveProjectileSelectionByItem[item] = selectedProjectile;
            ApplyCurrentBarrelPos(item, reportMissingActiveRule: true);
        }

        public static bool TryGetCurrentLocalPosition(Item item, out Vector2 localPosition)
        {
            localPosition = Vector2.Zero;
            if (item == null || item.Removed)
            {
                return false;
            }

            string activeKey = GetActiveRuleKey(item);
            if (CachedLocalPositions.TryGetValue(item, out CachedLocalPosition cached) &&
                cached.ActiveKey.Equals(activeKey, StringComparison.OrdinalIgnoreCase))
            {
                localPosition = cached.LocalPosition;
                return true;
            }

            if (!RulesByItem.TryGetValue(item, out ConcurrentDictionary<string, BarrelRule>? rules))
            {
                return false;
            }

            if (!rules.TryGetValue(activeKey, out BarrelRule rule) &&
                !rules.TryGetValue(PrimaryKey, out rule))
            {
                return false;
            }

            localPosition = rule.LocalPosition;
            CachedLocalPositions[item] = new CachedLocalPosition(activeKey, localPosition);
            return true;
        }

        private static bool ApplyCurrentBarrelPos(Item item, bool reportMissingActiveRule)
        {
            if (item == null || item.Removed)
            {
                return false;
            }

            if (!RulesByItem.TryGetValue(item, out ConcurrentDictionary<string, BarrelRule>? rules))
            {
                return false;
            }

            string activeKey = GetActiveRuleKey(item);
            if (!rules.TryGetValue(activeKey, out BarrelRule rule))
            {
                if (reportMissingActiveRule && activeKey.Equals(LowerRailKey, StringComparison.OrdinalIgnoreCase))
                {
                    ReportMissingActiveRuleOnce(item, activeKey, rules);
                }

                if (!rules.TryGetValue(PrimaryKey, out rule))
                {
                    return false;
                }
            }

            RangedWeapon? rangedWeapon = item.GetComponent<RangedWeapon>();
            if (rangedWeapon == null)
            {
                DebugConsole.ThrowError($"GunsmithFramework QAT barrel transform registered for a non-ranged weapon. weapon={item.Prefab.Identifier.Value}");
                return false;
            }

            rangedWeapon.BarrelPos = string.Create(
                CultureInfo.InvariantCulture,
                $"{rule.LocalPosition.X},{rule.LocalPosition.Y}");
            return true;
        }

        private static string GetActiveRuleKey(Item item)
        {
            if (ActiveRuleKeyByItem.TryGetValue(item, out string? key))
            {
                return key;
            }

            if (GunsmithQuickAttachmentBarrelSelectorPatch.TryGetSelectedProjectile(item, out int selectedProjectile))
            {
                key = KeyForProjectileSelection(selectedProjectile);
                ActiveRuleKeyByItem[item] = key;
                ActiveProjectileSelectionByItem[item] = selectedProjectile;
                return key;
            }

            return PrimaryKey;
        }

        private static string KeyForProjectileSelection(int selectedProjectile)
            => selectedProjectile == 1 ? LowerRailKey : PrimaryKey;

        private static string NormalizeKey(string? key)
            => string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().ToLowerInvariant();

        private static void ReportMissingActiveRuleOnce(Item item, string activeKey, ConcurrentDictionary<string, BarrelRule> rules)
        {
            string selected = ActiveProjectileSelectionByItem.TryGetValue(item, out int selectedProjectile)
                ? selectedProjectile.ToString(CultureInfo.InvariantCulture)
                : "unknown";
            string registeredKeys = rules.IsEmpty ? "<none>" : string.Join(", ", rules.Keys);
            string signature = string.Create(
                CultureInfo.InvariantCulture,
                $"{selected}|{activeKey}|{registeredKeys}");

            if (ReportedMissingRuleSignatureByItem.TryGetValue(item, out string? previousSignature) &&
                previousSignature.Equals(signature, StringComparison.Ordinal))
            {
                return;
            }

            ReportedMissingRuleSignatureByItem[item] = signature;
            DebugConsole.NewMessage(
                $"GunsmithFramework QAT selected a barrel rule that is not registered; falling back to primary. " +
                $"weapon={item.Prefab.Identifier.Value}, " +
                $"selectedProjectile={selected}, " +
                $"activeKey={activeKey}, " +
                $"registeredKeys={registeredKeys}",
                Color.Yellow,
                false);
        }

        private static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);

        private readonly record struct CachedLocalPosition(string ActiveKey, Vector2 LocalPosition);
        private readonly record struct BarrelRule(Vector2 LocalPosition, float RotationDegrees);
    }
}
