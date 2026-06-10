using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GunsmithFramework
{
    [HarmonyPatch]
    internal static class GunsmithFabricatorClientPatch
    {
        private static readonly ConditionalWeakTable<Fabricator, ClientState> States = new();
        private static readonly FieldInfo? ItemListField = AccessTools.Field(typeof(Fabricator), "itemList");
        private static readonly FieldInfo? ItemCategoryButtonsField = AccessTools.Field(typeof(Fabricator), "itemCategoryButtons");
        private static readonly FieldInfo? SelectedItemCategoryField = AccessTools.Field(typeof(Fabricator), "selectedItemCategory");
        private static readonly FieldInfo? ItemFilterBoxField = AccessTools.Field(typeof(Fabricator), "itemFilterBox");
        private static readonly FieldInfo? InputInventoryHolderField = AccessTools.Field(typeof(Fabricator), "inputInventoryHolder");
        private static readonly FieldInfo? ActivateButtonField = AccessTools.Field(typeof(Fabricator), "activateButton");
        private static readonly FieldInfo? NothingToShowTextField = AccessTools.Field(typeof(Fabricator), "nothingToShowText");
        private static readonly FieldInfo? SelectedItemField = AccessTools.Field(typeof(Fabricator), "selectedItem");
        private static readonly FieldInfo? PendingFabricatedItemField = AccessTools.Field(typeof(Fabricator), "pendingFabricatedItem");
        private static readonly FieldInfo? FabricatedItemField = AccessTools.Field(typeof(Fabricator), "fabricatedItem");
        private static readonly FieldInfo? SelectedItemFrameField = AccessTools.Field(typeof(Fabricator), "selectedItemFrame");
        private static readonly FieldInfo? SelectedItemReqsFrameField = AccessTools.Field(typeof(Fabricator), "selectedItemReqsFrame");
        private static readonly MethodInfo? FilterEntitiesMethod = AccessTools.Method(typeof(Fabricator), "FilterEntities");

        private sealed class ClientState
        {
            public GUIButton? GunsmithCategoryButton { get; set; }
            public bool GunsmithCategorySelected { get; set; }
            public bool RefreshingGunsmithList { get; set; }
            public GUIComponent? InputInventoryHolder { get; set; }
            public GUIComponent? WeaponArea { get; set; }
            public GUIComponent? WeaponInventoryHolder { get; set; }
            public string LastWeaponKey { get; set; } = "\0";
        }

        [HarmonyPatch(typeof(Fabricator), "CreateGUI")]
        [HarmonyPostfix]
        private static void AddWeaponSlot(Fabricator __instance)
        {
            if (!GunsmithFabricatorRecipeFilterPatch.IsEnabledFabricator(__instance) ||
                !GunsmithFabricatorRecipeFilterPatch.TryGetWeaponContainer(__instance, out ItemContainer? weaponContainer) ||
                weaponContainer == null)
            {
                return;
            }

            ClientState state = States.GetOrCreateValue(__instance);
            state.GunsmithCategoryButton = null;
            state.GunsmithCategorySelected = false;

            AddGunsmithCategoryButton(__instance, state);

            GUIComponent? inputInventoryHolder = InputInventoryHolderField?.GetValue(__instance) as GUIComponent;
            GUIComponent? inputArea = inputInventoryHolder?.Parent;
            if (inputInventoryHolder == null || inputArea == null)
            {
                return;
            }

            inputInventoryHolder.RectTransform.RelativeSize = new Vector2(0.55f, 1f);

            var weaponArea = new GUILayoutGroup(
                new RectTransform(new Vector2(0.15f, 1f), inputArea.RectTransform),
                childAnchor: Anchor.Center,
                isHorizontal: false)
            {
                Stretch = true,
                RelativeSpacing = 0.04f
            };

            var weaponInventoryHolder = new GUIFrame(
                new RectTransform(new Vector2(0.72f, 0.9f), weaponArea.RectTransform, Anchor.Center, scaleBasis: ScaleBasis.BothHeight),
                style: null);
            weaponArea.SetAsFirstChild();
            inputInventoryHolder.SetAsFirstChild();

            state.InputInventoryHolder = inputInventoryHolder;
            state.WeaponArea = weaponArea;
            state.WeaponInventoryHolder = weaponInventoryHolder;
            state.LastWeaponKey = GetWeaponKey(__instance);

            AttachWeaponContainerToGui(weaponContainer, weaponInventoryHolder);
            UpdateWeaponSlotVisibility(__instance);
        }

        [HarmonyPatch(typeof(Fabricator), "InitInventoryUIs")]
        [HarmonyPostfix]
        private static void ReattachWeaponSlotInventory(Fabricator __instance)
        {
            if (!GunsmithFabricatorRecipeFilterPatch.TryGetWeaponContainer(__instance, out ItemContainer? weaponContainer) ||
                weaponContainer == null ||
                !States.TryGetValue(__instance, out ClientState? state) ||
                state.WeaponInventoryHolder == null)
            {
                return;
            }

            AttachWeaponContainerToGui(weaponContainer, state.WeaponInventoryHolder);
            UpdateWeaponSlotVisibility(__instance);
        }

        [HarmonyPatch(typeof(Fabricator), "FilterEntities")]
        [HarmonyPostfix]
        private static void ApplyFilterAfterVanillaFiltering(Fabricator __instance)
        {
            if (!IsGunsmithCategorySelected(__instance))
            {
                UpdateCategoryButtonSelection(__instance);
                return;
            }

            ApplyGunsmithFilter(__instance);
        }

        [HarmonyPatch(typeof(Fabricator), "UpdateHUDComponentSpecific")]
        [HarmonyPostfix]
        private static void RefreshWhenWeaponSlotChanges(Fabricator __instance)
        {
            if (!GunsmithFabricatorRecipeFilterPatch.IsEnabledFabricator(__instance))
            {
                return;
            }

            ClientState state = States.GetOrCreateValue(__instance);
            string weaponKey = GetWeaponKey(__instance);
            if (!string.Equals(state.LastWeaponKey, weaponKey, StringComparison.Ordinal))
            {
                state.LastWeaponKey = weaponKey;
                if (state.GunsmithCategorySelected)
                {
                    RefreshRecipeList(__instance);
                }
            }

            object? selectedItem = SelectedItemField?.GetValue(__instance);
            if (state.GunsmithCategorySelected &&
                selectedItem != null &&
                FabricatedItemField?.GetValue(__instance) == null &&
                !GunsmithFabricatorRecipeFilterPatch.IsRecipeAllowedForFabricator(__instance, selectedItem))
            {
                ClearSelectedRecipe(__instance);
            }
        }

        [HarmonyPatch(typeof(Fabricator), "StartButtonClicked")]
        [HarmonyPrefix]
        private static bool BlockInvalidClientStart(Fabricator __instance, ref bool __result)
        {
            if (!GunsmithFabricatorRecipeFilterPatch.IsEnabledFabricator(__instance) ||
                !IsGunsmithCategorySelected(__instance) ||
                FabricatedItemField?.GetValue(__instance) != null)
            {
                return true;
            }

            object? selectedItem = SelectedItemField?.GetValue(__instance);
            if (GunsmithFabricatorRecipeFilterPatch.IsRecipeAllowedForFabricator(__instance, selectedItem))
            {
                return true;
            }

            __result = false;
            ClearSelectedRecipe(__instance);
            return false;
        }

        private static void AddGunsmithCategoryButton(Fabricator fabricator, ClientState state)
        {
            if (ItemCategoryButtonsField?.GetValue(fabricator) is not IList<GUIButton> categoryButtons ||
                categoryButtons.Count == 0)
            {
                return;
            }

            WrapOriginalCategoryButtons(fabricator, categoryButtons);

            GUIButton firstButton = categoryButtons[0];
            GUIComponent? categoryButtonContainer = firstButton.Parent;
            if (categoryButtonContainer == null)
            {
                return;
            }

            int buttonSize = Math.Min(categoryButtonContainer.Rect.Width, categoryButtonContainer.Rect.Height / (categoryButtons.Count + 1));
            buttonSize = Math.Max(1, buttonSize);
            foreach (GUIButton button in categoryButtons)
            {
                button.RectTransform.NonScaledSize = new Point(buttonSize);
                FitCategoryButtonToSprite(button);
            }

            var gunsmithButton = new GUIButton(
                new RectTransform(new Point(buttonSize), categoryButtonContainer.RectTransform),
                style: "CategoryButton.Weapon")
            {
                ToolTip = TextManager.Get("gunsmith.framework.ui.fabricator_category").Fallback("Gunsmith"),
                OnClicked = (button, _) =>
                {
                    state.GunsmithCategorySelected = !button.Selected;
                    SelectedItemCategoryField?.SetValue(fabricator, null);

                    if (state.GunsmithCategorySelected && ItemFilterBoxField?.GetValue(fabricator) is GUITextBox filterBox)
                    {
                        filterBox.Text = string.Empty;
                    }

                    RefreshRecipeList(fabricator);
                    return true;
                }
            };

            gunsmithButton.RectTransform.SizeChanged += () => FitCategoryButtonToSprite(gunsmithButton);
            FitCategoryButtonToSprite(gunsmithButton);
            state.GunsmithCategoryButton = gunsmithButton;
            UpdateCategoryButtonSelection(fabricator);
        }

        private static void WrapOriginalCategoryButtons(Fabricator fabricator, IEnumerable<GUIButton> categoryButtons)
        {
            foreach (GUIButton categoryButton in categoryButtons)
            {
                GUIButton.OnClickedHandler? originalOnClicked = categoryButton.OnClicked;
                if (originalOnClicked == null)
                {
                    continue;
                }

                categoryButton.OnClicked = (button, userData) =>
                {
                    if (States.TryGetValue(fabricator, out ClientState? state))
                    {
                        state.GunsmithCategorySelected = false;
                    }

                    bool result = originalOnClicked(button, userData);

                    if (States.TryGetValue(fabricator, out state))
                    {
                        state.GunsmithCategorySelected = false;
                    }
                    UpdateCategoryButtonSelection(fabricator);
                    return result;
                };
            }
        }

        private static void FitCategoryButtonToSprite(GUIButton button)
        {
            if (button.Frame.sprites == null ||
                !button.Frame.sprites.TryGetValue(GUIComponent.ComponentState.None, out var spriteList))
            {
                return;
            }

            var sprite = spriteList?.FirstOrDefault();
            if (sprite == null || sprite.Sprite.SourceRect.Width <= 0)
            {
                return;
            }

            button.RectTransform.NonScaledSize = new Point(
                button.Rect.Width,
                (int)(button.Rect.Width * ((float)sprite.Sprite.SourceRect.Height / sprite.Sprite.SourceRect.Width)));
        }

        private static void RefreshRecipeList(Fabricator fabricator)
        {
            if (States.TryGetValue(fabricator, out ClientState? state) &&
                state.GunsmithCategorySelected)
            {
                state.RefreshingGunsmithList = true;
                try
                {
                    FilterEntitiesMethod?.Invoke(fabricator, new object?[] { null, string.Empty });
                }
                finally
                {
                    state.RefreshingGunsmithList = false;
                }
                ApplyGunsmithFilter(fabricator);
                return;
            }

            string filter = ItemFilterBoxField?.GetValue(fabricator) is GUITextBox filterBox ? filterBox.Text : string.Empty;
            object? selectedCategory = SelectedItemCategoryField?.GetValue(fabricator);
            FilterEntitiesMethod?.Invoke(fabricator, new object?[] { selectedCategory, filter });
            UpdateCategoryButtonSelection(fabricator);
        }

        private static void AttachWeaponContainerToGui(ItemContainer weaponContainer, GUIComponent holder)
        {
            weaponContainer.AllowUIOverlap = true;
            weaponContainer.DrawInventory = true;
            weaponContainer.HideItems = true;
            weaponContainer.UILabel = string.Empty;
            weaponContainer.Inventory.DrawWhenEquipped = true;
            weaponContainer.Inventory.RectTransform = holder.RectTransform;
        }

        private static void ApplyGunsmithFilter(Fabricator fabricator)
        {
            if (!GunsmithFabricatorRecipeFilterPatch.IsEnabledFabricator(fabricator))
            {
                return;
            }

            GUIListBox? itemList = ItemListField?.GetValue(fabricator) as GUIListBox;
            GUITextBlock? nothingToShowText = NothingToShowTextField?.GetValue(fabricator) as GUITextBlock;
            if (itemList == null)
            {
                return;
            }

            Item? weapon = GunsmithFabricatorRecipeFilterPatch.GetWeaponItem(fabricator);
            HashSet<string> allowedPartItemIdentifiers = GunsmithFabricatorRecipeFilterPatch.GetAllowedPartItemIdentifiers(weapon);
            bool hasAllowedWeapon = weapon != null && allowedPartItemIdentifiers.Count > 0;
            bool anyVisible = false;

            foreach (GUIComponent child in itemList.Content.Children)
            {
                if (child.UserData is MapEntityCategory)
                {
                    child.Visible = false;
                    continue;
                }

                object? recipe = child.UserData;
                if (!GunsmithFabricatorRecipeFilterPatch.TryGetRecipeTargetIdentifier(recipe, out string targetIdentifier))
                {
                    continue;
                }

                bool visible = hasAllowedWeapon && child.Visible && allowedPartItemIdentifiers.Contains(targetIdentifier);
                child.Visible = visible;
                anyVisible |= visible;
            }

            object? selectedItem = SelectedItemField?.GetValue(fabricator);
            if (selectedItem != null &&
                FabricatedItemField?.GetValue(fabricator) == null &&
                !GunsmithFabricatorRecipeFilterPatch.IsRecipeAllowedForFabricator(fabricator, selectedItem))
            {
                ClearSelectedRecipe(fabricator);
            }

            GUIButton? activateButton = ActivateButtonField?.GetValue(fabricator) as GUIButton;
            if (activateButton != null && SelectedItemField?.GetValue(fabricator) == null)
            {
                activateButton.Enabled = false;
                activateButton.UserData = null;
            }

            if (nothingToShowText != null)
            {
                nothingToShowText.Text = GetEmptyStateText(weapon, allowedPartItemIdentifiers.Count);
                nothingToShowText.Visible = !anyVisible;
            }

            UpdateCategoryButtonSelection(fabricator);
        }

        private static bool IsGunsmithCategorySelected(Fabricator fabricator)
            => States.TryGetValue(fabricator, out ClientState? state) && state.GunsmithCategorySelected;

        private static void UpdateCategoryButtonSelection(Fabricator fabricator)
        {
            if (!States.TryGetValue(fabricator, out ClientState? state))
            {
                return;
            }

            if (state.GunsmithCategoryButton != null)
            {
                state.GunsmithCategoryButton.Selected = state.GunsmithCategorySelected;
            }
            UpdateWeaponSlotVisibility(fabricator, state);

            if (!state.GunsmithCategorySelected ||
                ItemCategoryButtonsField?.GetValue(fabricator) is not IEnumerable<GUIButton> categoryButtons)
            {
                return;
            }

            foreach (GUIButton categoryButton in categoryButtons)
            {
                categoryButton.Selected = false;
            }
        }

        private static void UpdateWeaponSlotVisibility(Fabricator fabricator)
        {
            if (States.TryGetValue(fabricator, out ClientState? state))
            {
                UpdateWeaponSlotVisibility(fabricator, state);
            }
        }

        private static void UpdateWeaponSlotVisibility(Fabricator fabricator, ClientState state)
        {
            bool visible = state.GunsmithCategorySelected;

            if (state.InputInventoryHolder != null)
            {
                state.InputInventoryHolder.RectTransform.RelativeSize = visible ? new Vector2(0.55f, 1f) : new Vector2(0.70f, 1f);
            }

            if (state.WeaponArea != null)
            {
                state.WeaponArea.Visible = visible;
                state.WeaponArea.IgnoreLayoutGroups = !visible;
            }

            if (state.WeaponInventoryHolder != null)
            {
                state.WeaponInventoryHolder.Visible = visible;
            }

            if (GunsmithFabricatorRecipeFilterPatch.TryGetWeaponContainer(fabricator, out ItemContainer? weaponContainer) &&
                weaponContainer != null)
            {
                weaponContainer.DrawInventory = visible;
                weaponContainer.Inventory.DrawWhenEquipped = visible;
            }

            if (state.InputInventoryHolder?.Parent is GUILayoutGroup inputLayout)
            {
                inputLayout.Recalculate();
            }
        }

        private static string GetWeaponKey(Fabricator fabricator)
        {
            Item? weapon = GunsmithFabricatorRecipeFilterPatch.GetWeaponItem(fabricator);
            return weapon == null ? string.Empty : $"{weapon.ID}:{weapon.Prefab.Identifier.Value}";
        }

        private static LocalizedString GetEmptyStateText(Item? weapon, int allowedPartCount)
        {
            if (weapon == null)
            {
                return TextManager.Get("gunsmith.framework.ui.fabricator_insert_weapon").Fallback("Insert a Gunsmith weapon.");
            }

            return allowedPartCount <= 0
                ? TextManager.Get("gunsmith.framework.ui.fabricator_no_compatible_parts").Fallback("No compatible gunsmith recipes.")
                : TextManager.Get("noitemsheader");
        }

        private static void ClearSelectedRecipe(Fabricator fabricator)
        {
            SelectedItemField?.SetValue(fabricator, null);
            PendingFabricatedItemField?.SetValue(fabricator, null);

            if (ActivateButtonField?.GetValue(fabricator) is GUIButton activateButton)
            {
                activateButton.Enabled = false;
                activateButton.UserData = null;
            }

            if (SelectedItemFrameField?.GetValue(fabricator) is GUIFrame selectedItemFrame)
            {
                selectedItemFrame.RectTransform.ClearChildren();
            }

            if (SelectedItemReqsFrameField?.GetValue(fabricator) is GUIFrame selectedItemReqsFrame)
            {
                selectedItemReqsFrame.RectTransform.ClearChildren();
            }
        }
    }
}
