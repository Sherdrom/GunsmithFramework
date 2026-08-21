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
            public Dictionary<GUIButton, (GUIButton.OnClickedHandler? OnClicked, Point Size)> OriginalCategoryButtons { get; } = new();
            public bool GunsmithCategorySelected { get; set; }
            public bool RefreshingGunsmithList { get; set; }
            public GUIComponent? InputInventoryHolder { get; set; }
            public Vector2? OriginalInputInventoryHolderSize { get; set; }
            public GUIComponent? WeaponArea { get; set; }
            public GUIButton? WeaponSlot { get; set; }
            public GUIButton? OpenButton { get; set; }
            public Item? SelectedWeapon { get; set; }
            public string LastWeaponKey { get; set; } = "\0";
        }

        internal static void Reset()
        {
            foreach (KeyValuePair<Fabricator, ClientState> pair in States)
            {
                ClientState state = pair.Value;
                foreach (KeyValuePair<GUIButton, (GUIButton.OnClickedHandler? OnClicked, Point Size)> original in state.OriginalCategoryButtons)
                {
                    original.Key.OnClicked = original.Value.OnClicked;
                    original.Key.RectTransform.NonScaledSize = original.Value.Size;
                }

                if (state.GunsmithCategoryButton != null)
                {
                    state.GunsmithCategoryButton.RectTransform.Parent = null;
                }
                if (state.WeaponArea != null)
                {
                    state.WeaponArea.RectTransform.Parent = null;
                }
                if (state.OpenButton != null)
                {
                    state.OpenButton.RectTransform.Parent = null;
                }
                if (state.InputInventoryHolder != null && state.OriginalInputInventoryHolderSize is Vector2 originalInputSize)
                {
                    state.InputInventoryHolder.RectTransform.RelativeSize = originalInputSize;
                }

                ClearSelectedWeapon(pair.Key, state);
                if (state.InputInventoryHolder?.Parent is GUILayoutGroup inputLayout)
                {
                    inputLayout.Recalculate();
                }
            }

            States.Clear();
        }

        [HarmonyPatch(typeof(Fabricator), "CreateGUI")]
        [HarmonyPostfix]
        private static void AddWeaponSlot(Fabricator __instance)
        {
            if (!GunsmithFabricatorRecipeFilterPatch.IsEnabledFabricator(__instance))
            {
                return;
            }

            ClientState state = States.GetOrCreateValue(__instance);
            ClearSelectedWeapon(__instance, state);
            state.WeaponSlot?.RectTransform.Parent = null;
            state.OpenButton?.RectTransform.Parent = null;
            state.WeaponArea?.RectTransform.Parent = null;
            state.WeaponSlot = null;
            state.OpenButton = null;
            state.WeaponArea = null;
            state.GunsmithCategoryButton = null;
            state.GunsmithCategorySelected = false;

            AddGunsmithCategoryButton(__instance, state);

            GUIComponent? inputInventoryHolder = InputInventoryHolderField?.GetValue(__instance) as GUIComponent;
            GUIComponent? inputArea = inputInventoryHolder?.Parent;
            if (inputInventoryHolder == null || inputArea == null)
            {
                return;
            }

            state.InputInventoryHolder = inputInventoryHolder;
            state.OriginalInputInventoryHolderSize = inputInventoryHolder.RectTransform.RelativeSize;
            inputInventoryHolder.RectTransform.RelativeSize = new Vector2(0.55f, 1f);

            var weaponArea = new GUILayoutGroup(
                new RectTransform(new Vector2(0.15f, 1f), inputArea.RectTransform),
                childAnchor: Anchor.Center,
                isHorizontal: false)
            {
                Stretch = true,
                RelativeSpacing = 0.04f
            };

            var weaponSlot = new GUIButton(
                new RectTransform(new Vector2(0.65f, 0.65f), weaponArea.RectTransform, Anchor.Center, scaleBasis: ScaleBasis.BothHeight),
                style: null)
            {
                ToolTip = TextManager.Get("gunsmith.framework.ui.fabricator_weapon_slot").Fallback("Weapon"),
                Color = Color.Black * 0.35f,
                OutlineColor = Color.Cyan * 0.65f,
                CanBeFocused = false
            };
            var openButton = new GUIButton(
                new RectTransform(new Vector2(0.95f, 0.2f), weaponArea.RectTransform, Anchor.BottomCenter),
                (LocalizedString)TextManager.Get("gunsmith.framework.ui.fabricator_open").Fallback("Open Gunsmith"),
                Alignment.Center)
            {
                ToolTip = TextManager.Get("gunsmith.framework.ui.fabricator_open").Fallback("Open Gunsmith")
            };
            openButton.OnClicked = (_, _) =>
            {
                OpenSelectedWeapon(__instance, state);
                return true;
            };
            weaponArea.SetAsFirstChild();
            inputInventoryHolder.SetAsFirstChild();

            state.WeaponArea = weaponArea;
            state.WeaponSlot = weaponSlot;
            state.OpenButton = openButton;
            state.LastWeaponKey = GetWeaponKey(state.SelectedWeapon);
            UpdateVirtualWeaponSlot(state);
            UpdateWeaponSlotVisibility(__instance);
        }

        internal static bool TryHandleVirtualWeaponDrop()
        {
            if (GUI.InputBlockingMenuOpen ||
                GunsmithGui.IsGunsmithWindowBlockingInput ||
                !Inventory.DraggingItems.Any() ||
                !PlayerInput.PrimaryMouseButtonReleased())
            {
                return false;
            }

            foreach (KeyValuePair<Fabricator, ClientState> pair in States)
            {
                ClientState state = pair.Value;
                if (pair.Key.GuiFrame?.Parent == null ||
                    !state.GunsmithCategorySelected ||
                    state.WeaponSlot is not { Visible: true } weaponSlot ||
                    !weaponSlot.Rect.Contains(PlayerInput.MousePosition))
                {
                    continue;
                }

                Item? draggedItem = Inventory.DraggingItems.FirstOrDefault();
                bool accepted = CanOpenFromWorkbench(draggedItem, Character.Controlled);
                CancelVirtualWeaponDrag();
                if (accepted)
                {
                    state.SelectedWeapon = draggedItem;
                    state.LastWeaponKey = GetWeaponKey(draggedItem);
                    UpdateVirtualWeaponSlot(state);
                    RefreshRecipeList(pair.Key);
                    SoundPlayer.PlayUISound(GUISoundType.PickItem);
                }
                else
                {
                    SoundPlayer.PlayUISound(GUISoundType.PickItemFail);
                }

                return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(Fabricator), "InitInventoryUIs")]
        [HarmonyPostfix]
        private static void RefreshVirtualWeaponSlot(Fabricator __instance)
        {
            if (!States.TryGetValue(__instance, out ClientState? state))
            {
                return;
            }

            EnsureSelectedWeaponValid(__instance, state);
            UpdateVirtualWeaponSlot(state);
            UpdateWeaponSlotVisibility(__instance);
        }

        [HarmonyPatch(typeof(Fabricator), "RemoveComponentSpecific")]
        [HarmonyPostfix]
        private static void ClearWhenFabricatorRemoved(Fabricator __instance)
        {
            if (States.TryGetValue(__instance, out ClientState? state))
            {
                ClearSelectedWeapon(__instance, state);
            }
        }

        [HarmonyPatch(typeof(Fabricator), "CreateSelectedItemUI")]
        [HarmonyPostfix]
        private static void AppendGunsmithRecipeStats(Fabricator __instance)
        {
            if (!IsGunsmithCategorySelected(__instance))
            {
                return;
            }

            object? selectedRecipe = SelectedItemField?.GetValue(__instance);
            if (!GunsmithFabricatorRecipeFilterPatch.TryGetRecipeTargetIdentifier(selectedRecipe, out string identifier))
            {
                return;
            }

            string? stats = GunsmithGui.GetFormattedItemStats(identifier, indent: string.Empty);
            if (stats == null)
            {
                return;
            }

            FieldInfo? frameField = __instance.GuiFrame.Rect.Height > GUI.IntScale(500)
                ? SelectedItemReqsFrameField
                : SelectedItemFrameField;
            if (frameField?.GetValue(__instance) is not GUIFrame detailFrame ||
                detailFrame.GetChild<GUILayoutGroup>() is not GUILayoutGroup detailLayout)
            {
                return;
            }

            GUITextBlock statsBlock = new GUITextBlock(
                new RectTransform(new Vector2(1.0f, 0.0f), detailLayout.RectTransform),
                RichString.Rich(stats),
                font: GUIStyle.SmallFont,
                wrap: true);

            GUITextBlock? requiredSkillsBlock = detailLayout.Children
                .OfType<GUITextBlock>()
                .FirstOrDefault(block => block.ToolTip == TextManager.Get("fabricatorrequiredskills.tooltip"));
            if (requiredSkillsBlock != null)
            {
                statsBlock.RectTransform.RepositionChildInHierarchy(detailLayout.GetChildIndex(requiredSkillsBlock));
            }
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
            if ((state.WeaponArea != null &&
                    (state.WeaponArea.Parent == null || __instance.GuiFrame?.Parent == null)) ||
                __instance.Item?.Removed == true)
            {
                bool wasSelected = state.SelectedWeapon != null;
                bool wasCategorySelected = state.GunsmithCategorySelected;
                state.GunsmithCategorySelected = false;
                ClearSelectedWeapon(__instance, state);
                if (wasSelected && wasCategorySelected)
                {
                    RefreshRecipeList(__instance);
                }
                return;
            }

            bool hadSelectedWeapon = state.SelectedWeapon != null;
            bool selectedWeaponValid = EnsureSelectedWeaponValid(__instance, state);
            if (!selectedWeaponValid && hadSelectedWeapon && state.GunsmithCategorySelected)
            {
                RefreshRecipeList(__instance);
            }

            string weaponKey = GetWeaponKey(state.SelectedWeapon);
            if (!string.Equals(state.LastWeaponKey, weaponKey, StringComparison.Ordinal))
            {
                state.LastWeaponKey = weaponKey;
                if (state.GunsmithCategorySelected)
                {
                    RefreshRecipeList(__instance);
                }
            }
            UpdateVirtualWeaponSlot(state);
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

            if (!States.TryGetValue(__instance, out ClientState? state) ||
                !EnsureSelectedWeaponValid(__instance, state) ||
                !IsRecipeAllowedForSelectedWeapon(state.SelectedWeapon, SelectedItemField?.GetValue(__instance)))
            {
                __result = false;
                ClearSelectedRecipe(__instance);
                return false;
            }

            return true;
        }

        private static void AddGunsmithCategoryButton(Fabricator fabricator, ClientState state)
        {
            if (ItemCategoryButtonsField?.GetValue(fabricator) is not IList<GUIButton> categoryButtons ||
                categoryButtons.Count == 0)
            {
                return;
            }

            WrapOriginalCategoryButtons(fabricator, state, categoryButtons);

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
                    if (!state.GunsmithCategorySelected)
                    {
                        ClearSelectedWeapon(fabricator, state);
                    }
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

        private static void WrapOriginalCategoryButtons(Fabricator fabricator, ClientState state, IEnumerable<GUIButton> categoryButtons)
        {
            foreach (GUIButton categoryButton in categoryButtons)
            {
                if (state.OriginalCategoryButtons.ContainsKey(categoryButton))
                {
                    continue;
                }

                GUIButton.OnClickedHandler? originalOnClicked = categoryButton.OnClicked;
                state.OriginalCategoryButtons[categoryButton] = (originalOnClicked, categoryButton.RectTransform.NonScaledSize);
                if (originalOnClicked == null)
                {
                    continue;
                }

                categoryButton.OnClicked = (button, userData) =>
                {
                    if (States.TryGetValue(fabricator, out ClientState? state))
                    {
                        state.GunsmithCategorySelected = false;
                        ClearSelectedWeapon(fabricator, state);
                    }

                    bool result = originalOnClicked(button, userData);

                    if (States.TryGetValue(fabricator, out state))
                    {
                        state.GunsmithCategorySelected = false;
                        ClearSelectedWeapon(fabricator, state);
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

        private static void OpenSelectedWeapon(Fabricator fabricator, ClientState state)
        {
            if (!EnsureSelectedWeaponValid(fabricator, state) ||
                Character.Controlled is not { } character ||
                fabricator.Item is not { Removed: false } workbench)
            {
                return;
            }
            GunsmithApi.CallLuaHook(
                "GunsmithFrameworkOpenFromWorkbench",
                state.SelectedWeapon!,
                workbench,
                character);
        }

        private static bool EnsureSelectedWeaponValid(Fabricator fabricator, ClientState state)
        {
            if (state.SelectedWeapon == null)
            {
                return false;
            }

            Character? character = Character.Controlled;
            if (CanOpenFromWorkbench(state.SelectedWeapon, character))
            {
                return true;
            }

            ClearSelectedWeapon(fabricator, state);
            return false;
        }

        private static bool CanOpenFromWorkbench(Item? weapon, Character? character)
            => weapon != null &&
               character != null &&
               !weapon.Removed &&
               weapon.IsOwnedBy(character) &&
               GunsmithLuaHooks.Call("GunsmithFrameworkCanOpenFromWorkbench", weapon) is true;

        private static bool IsRecipeAllowedForSelectedWeapon(Item? weapon, object? recipe)
        {
            if (weapon == null ||
                recipe == null ||
                !GunsmithFabricatorRecipeFilterPatch.TryGetRecipeTargetIdentifier(recipe, out string targetIdentifier))
            {
                return false;
            }

            HashSet<string> allowedIdentifiers = GunsmithFabricatorRecipeFilterPatch.GetAllowedPartItemIdentifiers(weapon);
            return allowedIdentifiers.Count > 0 && allowedIdentifiers.Contains(targetIdentifier);
        }

        private static void ClearSelectedWeapon(Fabricator fabricator, ClientState state)
        {
            state.SelectedWeapon = null;
            state.LastWeaponKey = string.Empty;
            UpdateVirtualWeaponSlot(state);
            if (fabricator.Item is { } workbench)
            {
                GunsmithGui.CloseForWorkbench(workbench);
            }
        }

        internal static void CancelVirtualWeaponDrag()
        {
            Inventory.DraggingItems.Clear();
            Inventory.DraggingSlot = null;
        }

        private static void UpdateVirtualWeaponSlot(ClientState state)
        {
            if (state.WeaponSlot == null)
            {
                return;
            }

            if (state.SelectedWeapon is { Removed: false } weapon)
            {
                state.WeaponSlot.Text = (LocalizedString)weapon.Name;
                state.WeaponSlot.ToolTip = (LocalizedString)weapon.Name;
                state.WeaponSlot.Selected = true;
            }
            else
            {
                state.WeaponSlot.Text = TextManager.Get("gunsmith.framework.ui.fabricator_weapon_slot").Fallback("Weapon");
                state.WeaponSlot.ToolTip = TextManager.Get("gunsmith.framework.ui.fabricator_insert_weapon").Fallback("Drag a workbench-only Gunsmith weapon to this virtual slot.");
                state.WeaponSlot.Selected = false;
            }

            if (state.OpenButton != null)
            {
                state.OpenButton.Enabled = state.SelectedWeapon != null && CanOpenFromWorkbench(state.SelectedWeapon, Character.Controlled);
            }
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

            States.TryGetValue(fabricator, out ClientState? state);
            if (state != null)
            {
                EnsureSelectedWeaponValid(fabricator, state);
            }

            Item? weapon = state?.SelectedWeapon;
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
                !IsRecipeAllowedForSelectedWeapon(weapon, selectedItem))
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

            if (!visible)
            {
                ClearSelectedWeapon(fabricator, state);
            }

            if (state.InputInventoryHolder != null)
            {
                state.InputInventoryHolder.RectTransform.RelativeSize = visible ? new Vector2(0.55f, 1f) : new Vector2(0.70f, 1f);
            }

            if (state.WeaponArea != null)
            {
                state.WeaponArea.Visible = visible;
                state.WeaponArea.IgnoreLayoutGroups = !visible;
            }

            if (state.WeaponSlot != null)
            {
                state.WeaponSlot.Visible = visible;
                state.WeaponSlot.Enabled = visible;
            }
            if (state.OpenButton != null)
            {
                state.OpenButton.Visible = visible;
                state.OpenButton.Enabled = visible && state.SelectedWeapon != null && CanOpenFromWorkbench(state.SelectedWeapon, Character.Controlled);
            }

            if (state.InputInventoryHolder?.Parent is GUILayoutGroup inputLayout)
            {
                inputLayout.Recalculate();
            }
        }

        private static string GetWeaponKey(Item? weapon)
            => weapon == null ? string.Empty : $"{weapon.ID}:{weapon.Prefab.Identifier.Value}";

        private static LocalizedString GetEmptyStateText(Item? weapon, int allowedPartCount)
        {
            if (weapon == null)
            {
                return TextManager.Get("gunsmith.framework.ui.fabricator_insert_weapon").Fallback("Drag a workbench-only Gunsmith weapon to this virtual slot.");
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
