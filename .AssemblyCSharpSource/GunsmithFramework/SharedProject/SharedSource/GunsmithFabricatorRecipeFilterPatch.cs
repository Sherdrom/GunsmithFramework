using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace GunsmithFramework
{
    internal static class GunsmithFabricatorRecipeFilterPatch
    {
        internal const string EnableAttributeName = "gunsmithframeworkbutton";
        internal const string LuaHookName = "GunsmithFrameworkGetFabricatorPartItemIds";

        private static readonly HashSet<string> DefaultEnabledFabricatorPrefabIdentifiers = new(StringComparer.OrdinalIgnoreCase)
        {
            "fabricator"
        };
        private static readonly char[] PartIdentifierSeparators = { ',', ';', '|', '\n', '\r', '\t' };
        private static readonly Type? FabricationRecipeType = AccessTools.TypeByName("Barotrauma.FabricationRecipe");
        private static readonly PropertyInfo? RecipeTargetItemProperty = FabricationRecipeType == null ? null : AccessTools.Property(FabricationRecipeType, "TargetItem");
        private static readonly FieldInfo? RecipeTargetItemPrefabIdentifierField = FabricationRecipeType == null ? null : AccessTools.Field(FabricationRecipeType, "TargetItemPrefabIdentifier");

        internal static bool IsEnabledFabricator(Fabricator? fabricator)
            => IsEnabledPrefab(fabricator?.Item?.Prefab);

        internal static bool IsEnabledPrefab(ItemPrefab? itemPrefab)
        {
            if (itemPrefab == null)
            {
                return false;
            }

            return IsDefaultEnabledPrefabIdentifier(itemPrefab.Identifier.Value) ||
                   IsEnabledConfigElement(itemPrefab.ConfigElement);
        }

        internal static bool IsDefaultEnabledPrefabIdentifier(string? identifier)
            => !string.IsNullOrWhiteSpace(identifier) &&
               DefaultEnabledFabricatorPrefabIdentifiers.Contains(identifier);

        internal static bool IsEnabledConfigElement(ContentXElement? configElement)
            => IsEnabledElement(configElement?.Element);

        internal static bool IsEnabledElement(XElement? configElement)
        {
            if (configElement == null)
            {
                return false;
            }

            return configElement.Elements().Any(IsEnabledFabricatorElement);
        }

        internal static HashSet<string> GetAllowedPartItemIdentifiers(Item? weapon)
        {
            HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase);
            if (weapon == null)
            {
                return identifiers;
            }

            try
            {
                if (LuaCsSetup.Instance?.Hook is Barotrauma.LuaCs.Compatibility.ILuaCsHook hook)
                {
                    object? result = hook.Call(LuaHookName, weapon);
                    identifiers.UnionWith(ParsePartItemIdentifierResult(result));
                }
            }
            catch (Exception ex)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to query fabricator part item ids: {ex.Message}");
            }

            return identifiers;
        }

        internal static HashSet<string> ParsePartItemIdentifierResult(object? result)
        {
            HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase);
            CollectHookResultIdentifiers(result, identifiers);
            return identifiers;
        }

        internal static bool TryGetRecipeTargetIdentifier(object? recipe, out string identifier)
        {
            identifier = string.Empty;
            if (recipe == null)
            {
                return false;
            }

            if (RecipeTargetItemProperty?.GetValue(recipe) is ItemPrefab targetItem)
            {
                identifier = targetItem.Identifier.Value;
                return !string.IsNullOrWhiteSpace(identifier);
            }

            object? targetIdentifier = RecipeTargetItemPrefabIdentifierField?.GetValue(recipe);
            if (targetIdentifier != null)
            {
                identifier = targetIdentifier.ToString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(identifier);
            }

            return false;
        }

        private static bool IsEnabledFabricatorElement(XElement element)
        {
            return string.Equals(element.Name.LocalName, "fabricator", StringComparison.OrdinalIgnoreCase) &&
                   IsTruthyAttributeValue((string?)element.Attribute(EnableAttributeName));
        }

        private static bool IsTruthyAttributeValue(string? value)
        {
            if (bool.TryParse(value, out bool boolValue))
            {
                return boolValue;
            }

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
        }

        private static void CollectHookResultIdentifiers(object? result, HashSet<string> identifiers)
        {
            switch (result)
            {
                case null:
                    return;
                case string value:
                    AddIdentifiers(value, identifiers);
                    return;
                case IEnumerable enumerable:
                    foreach (object? entry in enumerable)
                    {
                        CollectHookResultIdentifiers(entry, identifiers);
                    }
                    return;
                default:
                    AddIdentifiers(result.ToString() ?? string.Empty, identifiers);
                    return;
            }
        }

        private static void AddIdentifiers(string spec, HashSet<string> identifiers)
        {
            foreach (string part in spec.Split(PartIdentifierSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    identifiers.Add(part);
                }
            }
        }
    }
}
