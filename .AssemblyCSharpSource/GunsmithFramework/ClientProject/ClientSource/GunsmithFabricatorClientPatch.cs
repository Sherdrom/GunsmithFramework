using Barotrauma;
using Barotrauma.Items.Components;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        private static readonly FieldInfo? SortByDropdownField = AccessTools.Field(typeof(Fabricator), "sortByDropdown");
        private static readonly FieldInfo? RequiresRecipeTextField = AccessTools.Field(typeof(Fabricator), "requiresRecipeText");
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
            public object? OriginalSortMode { get; set; }
            public Dictionary<(bool MissingRecipe, string PartType), GUITextBlock> PartCategoryHeaders { get; } = new();
            public GUIComponent? InputInventoryHolder { get; set; }
            public Vector2? OriginalInputInventoryHolderSize { get; set; }
            public GUIComponent? WeaponArea { get; set; }
            public GUIComponent? WeaponSlot { get; set; }
            public VisualSlot WeaponVisualSlot { get; } = new(Rectangle.Empty);
            public Inventory.SlotReference? WeaponTooltipSlot { get; set; }
            public bool DraggingWeaponSelection { get; set; }
            public GUIButton? OpenButton { get; set; }
            public Item? SelectedWeapon { get; set; }
            public string LastWeaponKey { get; set; } = "\0";
        }

        internal static void Reset()
        {
            foreach (KeyValuePair<Fabricator, ClientState> pair in States)
            {
                ClientState state = pair.Value;
                state.GunsmithCategorySelected = false;
                ClearPartCategoryHeaders(state);
                RestoreSortMode(pair.Key, state);
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
            ClearPartCategoryHeaders(state);
            state.OriginalSortMode = null;
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
                new RectTransform(new Vector2(0.15f, 0.8f), inputArea.RectTransform),
                childAnchor: Anchor.TopCenter,
                isHorizontal: false)
            {
                RelativeSpacing = 0.04f
            };

            var weaponSlot = new GUICustomComponent(
                new RectTransform(new Vector2(0.65f, 0.65f), weaponArea.RectTransform, Anchor.Center, scaleBasis: ScaleBasis.BothHeight),
                (spriteBatch, component) => DrawWeaponSlot(spriteBatch, component, state))
            {
                OnDrawToolTip = _ => UpdateWeaponSlotTooltip(state),
                HoverCursor = CursorState.Hand
            };
            var openButton = new GUIButton(
                new RectTransform(new Vector2(0.85f, 0.28f), weaponArea.RectTransform, Anchor.BottomCenter),
                (LocalizedString)TextManager.Get("gunsmith.framework.ui.fabricator_open").Fallback("Open Gunsmith"),
                Alignment.Center)
            {
                ToolTip = TextManager.Get("gunsmith.framework.ui.fabricator_open").Fallback("Open Gunsmith"),
                Font = GUIStyle.SmallFont
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
            if (Character.Controlled?.SelectedItem is not { } selectedItem ||
                GUI.InputBlockingMenuOpen ||
                GunsmithGui.IsGunsmithWindowBlockingInput)
            {
                foreach (var pair in States) { pair.Value.DraggingWeaponSelection = false; }
                return false;
            }

            foreach (KeyValuePair<Fabricator, ClientState> pair in States)
            {
                ClientState state = pair.Value;
                // MouseOn stays captured by the drag origin through the release frame.
                if (pair.Key.GuiFrame is not { Visible: true } frame ||
                    frame.RectTransform.Parent == null ||
                    !selectedItem.ActiveHUDs.Contains(pair.Key) ||
                    !state.GunsmithCategorySelected ||
                    state.WeaponSlot is not { Visible: true } weaponSlot)
                {
                    state.DraggingWeaponSelection = false;
                    continue;
                }

                bool mouseOnSlot = weaponSlot.Rect.Contains(PlayerInput.MousePosition);
                if (state.DraggingWeaponSelection)
                {
                    if (!PlayerInput.PrimaryMouseButtonHeld())
                    {
                        state.DraggingWeaponSelection = false;
                        if (PlayerInput.PrimaryMouseButtonClicked() && !mouseOnSlot)
                        {
                            ClearSelectedWeapon(pair.Key, state);
                            RefreshRecipeList(pair.Key);
                        }
                    }
                    return true;
                }

                if (!Inventory.DraggingItems.Any())
                {
                    if (state.SelectedWeapon is { Removed: false } && mouseOnSlot &&
                        GUI.IsMouseOn(weaponSlot) && PlayerInput.PrimaryMouseButtonDown())
                    {
                        // Only drag the selection; never expose the real weapon to inventory drop handlers.
                        state.DraggingWeaponSelection = true;
                        return true;
                    }
                    continue;
                }
                if (!mouseOnSlot || !PlayerInput.PrimaryMouseButtonReleased()) { continue; }

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

        [HarmonyPatch(typeof(Fabricator), "SortItems")]
        [HarmonyPostfix]
        private static void GroupPartsAfterVanillaSorting(Fabricator __instance)
        {
            if (IsGunsmithCategorySelected(__instance)) { ApplyGunsmithFilter(__instance); }
        }

        [HarmonyPatch(typeof(Fabricator), "HideEmptyItemListCategories")]
        [HarmonyPrefix]
        private static bool KeepPartCategoryHeaders(Fabricator __instance)
            => !IsGunsmithCategorySelected(__instance) || !IsSortingByCategory(__instance);

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
                    (state.WeaponArea.Parent == null || __instance.GuiFrame?.RectTransform.Parent == null)) ||
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
                    if (state.GunsmithCategorySelected && SortByDropdownField?.GetValue(fabricator) is GUIDropDown sortBy)
                    {
                        state.OriginalSortMode = sortBy.SelectedData;
                        sortBy.Select(index: 0);
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
            state.DraggingWeaponSelection = false;
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
            if (state.WeaponTooltipSlot?.Item != state.SelectedWeapon)
            {
                state.WeaponTooltipSlot = null;
            }

            if (state.OpenButton != null)
            {
                state.OpenButton.Enabled = state.SelectedWeapon != null && CanOpenFromWorkbench(state.SelectedWeapon, Character.Controlled);
            }
        }

        private static void DrawWeaponSlot(SpriteBatch spriteBatch, GUIComponent component, ClientState state)
        {
            VisualSlot slot = state.WeaponVisualSlot;
            slot.Rect = slot.InteractRect = component.Rect;
            Item? weapon = state.SelectedWeapon is { Removed: false } selected ? selected : null;
            Item? dragged = Inventory.DraggingItems.FirstOrDefault();
            bool hovered = !GUI.InputBlockingMenuOpen && component.Rect.Contains(PlayerInput.MousePosition) &&
                (dragged != null || GUI.IsMouseOn(component));
            slot.HighlightColor = !hovered ? Color.Transparent : dragged == null ? Color.White * 0.35f :
                CanOpenFromWorkbench(dragged, Character.Controlled) ? GUIStyle.Green : GUIStyle.Red;
            Inventory.DrawSlot(spriteBatch, null, slot, weapon, -1,
                drawItem: !state.DraggingWeaponSelection || component.Rect.Contains(PlayerInput.MousePosition));
            if (!hovered || dragged != null)
            {
                state.WeaponTooltipSlot = null;
            }
        }

        private static void UpdateWeaponSlotTooltip(ClientState state)
        {
            if (state.WeaponSlot == null) { return; }
            state.WeaponSlot.ToolTip = string.Empty;
            if (state.DraggingWeaponSelection || Inventory.DraggingItems.Any()) { return; }
            if (state.SelectedWeapon is not { Removed: false, ParentInventory: { } inventory } weapon)
            {
                state.WeaponSlot.ToolTip = TextManager.Get("gunsmith.framework.ui.fabricator_insert_weapon")
                    .Fallback("Drag a Gunsmith weapon here to select it. The weapon stays in your inventory.");
                return;
            }

            int slotIndex = inventory.FindIndex(weapon);
            if (slotIndex < 0) { return; }
            if (state.WeaponTooltipSlot == null || state.WeaponTooltipSlot.Item != weapon ||
                state.WeaponTooltipSlot.ParentInventory != inventory || state.WeaponTooltipSlot.SlotIndex != slotIndex)
            {
                state.WeaponTooltipSlot = new Inventory.SlotReference(inventory, state.WeaponVisualSlot, slotIndex, isSubSlot: false);
            }
            else if (state.WeaponTooltipSlot.TooltipNeedsRefresh())
            {
                state.WeaponTooltipSlot.RefreshTooltip();
            }
            state.WeaponSlot.ToolTip = RichString.Rich(
                TextManager.Get("gunsmith.framework.ui.fabricator_selected_weapon")
                    .Fallback("Modding target (still in your inventory). Drag outside this box to clear the selection.") +
                "\n\n" + state.WeaponTooltipSlot.Tooltip.NestedStr);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.DrawFront))]
        [HarmonyPrefix]
        private static void SuppressWorldDropLabelOverWeaponSlot(out bool __state)
        {
            __state = false;
            if (GUI.MouseOn != null || !Inventory.DraggingItems.Any() ||
                Character.Controlled?.SelectedItem is not { } selectedItem ||
                GUI.InputBlockingMenuOpen || GunsmithGui.IsGunsmithWindowBlockingInput) { return; }

            foreach (var pair in States)
            {
                if (pair.Key.GuiFrame is not { Visible: true } frame || frame.RectTransform.Parent == null ||
                    !selectedItem.ActiveHUDs.Contains(pair.Key) || !pair.Value.GunsmithCategorySelected ||
                    pair.Value.WeaponSlot is not { Visible: true } slot || !slot.Rect.Contains(PlayerInput.MousePosition))
                {
                    continue;
                }
                // Only change drawing context; input must retain the drag origin's mouse capture.
                __state = true;
                GUI.ForceMouseOn(slot);
                return;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.DrawFront))]
        [HarmonyFinalizer]
        private static void RestoreMouseAfterWeaponDragDrawing(bool __state)
        {
            if (__state) { GUI.ForceMouseOn(null); }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.DrawFront))]
        [HarmonyPostfix]
        private static void DrawWeaponSelectionDrag(SpriteBatch spriteBatch)
        {
            if (GUI.InputBlockingMenuOpen || GunsmithGui.IsGunsmithWindowBlockingInput) { return; }
            foreach (var pair in States)
            {
                ClientState state = pair.Value;
                if (!state.DraggingWeaponSelection || state.SelectedWeapon is not { Removed: false } weapon ||
                    state.WeaponSlot is not { Visible: true } slot || slot.Rect.Contains(PlayerInput.MousePosition) ||
                    Character.Controlled?.SelectedItem?.ActiveHUDs.Contains(pair.Key) != true)
                {
                    continue;
                }
                Sprite? sprite = weapon.OverrideInventorySprite ?? weapon.Prefab.InventoryIcon ?? weapon.Sprite;
                if (sprite == null) { continue; }
                float size = 64 * GUI.Scale;
                float scale = Math.Min(Math.Min(size / sprite.size.X, size / sprite.size.Y), 1.5f);
                sprite.Draw(spriteBatch, PlayerInput.MousePosition, weapon.GetInventoryIconColor(), scale: scale);
                GUI.DrawString(spriteBatch, PlayerInput.MousePosition + new Vector2(size * 0.5f, 0),
                    TextManager.Get("gunsmith.framework.ui.fabricator_clear_selection").Fallback("Release to clear selection"),
                    Color.LightYellow, Color.Black * 0.65f, 0, GUIStyle.SmallFont);
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
            var partCategories = ParsePartCategories(weapon == null ? null :
                GunsmithLuaHooks.Call(GunsmithFabricatorRecipeFilterPatch.LuaHookName, weapon, true) as string);
            var allowedPartItemIdentifiers = partCategories.Keys;
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

            if (state != null) { GroupPartRecipes(fabricator, itemList, state, partCategories); }
            UpdateCategoryButtonSelection(fabricator);
        }

        internal static Dictionary<string, (string PartType, string NameKey)> ParsePartCategories(string? spec)
        {
            Dictionary<string, (string PartType, string NameKey)> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (string entry in (spec ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = entry.Split(':');
                if (fields.Length != 3 || string.IsNullOrWhiteSpace(fields[0])) { continue; }
                result[Uri.UnescapeDataString(fields[0])] = (Uri.UnescapeDataString(fields[1]), Uri.UnescapeDataString(fields[2]));
            }
            return result;
        }

        private static bool IsSortingByCategory(Fabricator fabricator)
            => (SortByDropdownField?.GetValue(fabricator) as GUIDropDown)?.SelectedData?.ToString() == "Category";

        private static void RestoreSortMode(Fabricator fabricator, ClientState state)
        {
            if (state.OriginalSortMode is not { } original) { return; }
            state.OriginalSortMode = null;
            (SortByDropdownField?.GetValue(fabricator) as GUIDropDown)?.SelectItem(original);
        }

        private static void ClearPartCategoryHeaders(ClientState state)
        {
            foreach (GUITextBlock header in state.PartCategoryHeaders.Values) { header.RectTransform.Parent = null; }
            state.PartCategoryHeaders.Clear();
        }

        private static LocalizedString PartCategoryName((string PartType, string NameKey) category)
            => string.IsNullOrEmpty(category.PartType)
                ? TextManager.Get("gunsmith.framework.ui.fabricator_other_parts").Fallback("Other parts")
                : TextManager.Get(category.NameKey)
                    .Fallback(TextManager.Get("gunsmith.framework.parttype." + category.PartType.ToLowerInvariant()))
                    .Fallback(category.PartType);

        private static void GroupPartRecipes(Fabricator fabricator, GUIListBox itemList, ClientState state,
            IReadOnlyDictionary<string, (string PartType, string NameKey)> categories)
        {
            if (!IsSortingByCategory(fabricator))
            {
                ClearPartCategoryHeaders(state);
                return;
            }
            foreach (GUITextBlock header in state.PartCategoryHeaders.Values) { header.Visible = false; }
            var groups = itemList.Content.Children
                .Where(child => child.Visible && child.UserData is FabricationRecipe)
                .Select(child => (Child: child, Recipe: (FabricationRecipe)child.UserData))
                .Where(row => categories.ContainsKey(row.Recipe.TargetItemPrefabIdentifier.Value))
                .Select(row => (row.Child, row.Recipe, Category: categories[row.Recipe.TargetItemPrefabIdentifier.Value]))
                .GroupBy(row => (MissingRecipe: fabricator.MissingRequiredRecipe(row.Recipe, Character.Controlled), row.Category.PartType))
                .OrderBy(group => group.Key.MissingRecipe)
                .ThenBy(group => PartCategoryName(group.First().Category).Value, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            GUIComponent? requiresRecipeText = RequiresRecipeTextField?.GetValue(fabricator) as GUIComponent;
            if (requiresRecipeText != null)
            {
                requiresRecipeText.Visible = false;
                requiresRecipeText.RectTransform.SetAsLastChild();
            }
            int index = 0;
            foreach (var group in groups)
            {
                if (group.Key.MissingRecipe && requiresRecipeText is { Visible: false })
                {
                    requiresRecipeText.RectTransform.RepositionChildInHierarchy(index++);
                    requiresRecipeText.Visible = true;
                }
                if (!state.PartCategoryHeaders.TryGetValue(group.Key, out GUITextBlock? header))
                {
                    header = new GUITextBlock(new RectTransform(new Vector2(1f, 0.09f), itemList.Content.RectTransform),
                        PartCategoryName(group.First().Category), font: GUIStyle.SubHeadingFont, textColor: GUIStyle.TextColorBright)
                    {
                        CanBeFocused = false,
                        AutoScaleHorizontal = true,
                        AutoScaleVertical = true
                    };
                    new GUIFrame(new RectTransform(new Vector2(1f, 0.04f), header.RectTransform, Anchor.BottomCenter), style: "HorizontalLine")
                    {
                        CanBeFocused = false
                    };
                    state.PartCategoryHeaders.Add(group.Key, header);
                }
                header.Text = PartCategoryName(group.First().Category);
                header.Visible = true;
                header.RectTransform.RepositionChildInHierarchy(index++);
                foreach (var row in group.OrderBy(row => row.Recipe.DisplayName.Value, StringComparer.CurrentCultureIgnoreCase))
                {
                    row.Child.RectTransform.RepositionChildInHierarchy(index++);
                }
            }
            itemList.RecalculateChildren();
            itemList.UpdateScrollBarSize();
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
            if (!state.GunsmithCategorySelected)
            {
                ClearPartCategoryHeaders(state);
                RestoreSortMode(fabricator, state);
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
                return TextManager.Get("gunsmith.framework.ui.fabricator_insert_weapon").Fallback("Drag a Gunsmith weapon from your inventory to this virtual slot.");
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
