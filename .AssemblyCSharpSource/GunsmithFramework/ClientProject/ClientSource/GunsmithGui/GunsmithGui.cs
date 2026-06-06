namespace GunsmithFramework
{
    public static partial class GunsmithGui
    {
        private const string EmptyPartId = "__empty";
        private const string DefaultLocalizationPrefix = "gunsmith.framework";

        private static GUIFrame? activeWindow;
        private static Item? activeItem;
        private static string activeLocalizationPrefix = DefaultLocalizationPrefix;
        private static string? selectedSlot;
        private static GunsmithGuiContext activeContext = GunsmithGuiContext.Empty;
        private static List<GunsmithGuiSlot> activeSlots = new();
        private static GUIListBox? slotList;
        private static GUIFrame? previewPanel;
        private static GUIFrame? detailPanel;
        private static GUIListBox? partList;
        private static GUIFrame? partDetailPanel;
        private static GUITextBlock? partListTitle;
        private static readonly Dictionary<string, GunsmithPartRow> partRows = new(StringComparer.Ordinal);
        private static GUIButton? partDetailActionButton;
        private static string? partListSlotPath;
        private static string? partDetailSlotPath;
        private static string? selectedPartId;
        private static GunsmithPreviewSettings activePreviewSettings = GunsmithPreviewSettings.Default;
        private static GunsmithStats activeWeaponStats = GunsmithStats.Empty;
        private static bool activeQuickMode;
        private static QuickOverlayFrame? quickOverlayFrame;
        private static bool suppressQuickUninstallRelease;
        private static PendingQuickDrag? pendingQuickDrag;
        private static bool handlingNativeQuickDragDrop;
        private static Item? pendingNativeQuickDragDropClearItem;
        private static readonly HashSet<string> warnedQuickAnchorPaths = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Rectangle> partIconSourceCache = new(StringComparer.Ordinal);

        public static void OpenFromLua(Item item, string title, string slotSpec)
            => OpenInternal(item, title, slotSpec, quickMode: false);

        public static void OpenQuickFromLua(Item item, string title, string slotSpec)
            => OpenInternal(item, title, slotSpec, quickMode: true);

        private static void OpenInternal(Item item, string title, string slotSpec, bool quickMode)
        {
            if (item == null || item.Removed) { return; }

            GunsmithGuiSpec spec = ParseSpec(slotSpec);
            if (spec.Slots.Count == 0) { return; }
            activeLocalizationPrefix = LocalizationPrefixFromTitle(title);

            if (activeWindow != null && ReferenceEquals(activeItem, item) && activeQuickMode != quickMode)
            {
                CloseWindow();
            }

            if (activeWindow != null && ReferenceEquals(activeItem, item))
            {
                activeQuickMode = quickMode;
                string previousPath = activeContext.CurrentPath;
                activeContext = spec.Context;
                activeSlots = spec.Slots;
                activePreviewSettings = spec.PreviewSettings;
                activeWeaponStats = spec.WeaponStats;
                if (quickMode)
                {
                    RebuildQuickOverlay(title);
                }
                else
                {
                    SelectSlotAfterRefresh(previousPath, activeContext.CurrentPath);
                    RebuildSlotList();
                    RefreshSelectionPanels();
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedSlot) || spec.Slots.All(slot => slot.Path != selectedSlot))
            {
                selectedSlot = spec.Slots[0].Path;
            }

            CloseWindow();

            activeItem = item;
            activeQuickMode = quickMode;
            activeContext = spec.Context;
            activeSlots = spec.Slots;
            activePreviewSettings = spec.PreviewSettings;
            activeWeaponStats = spec.WeaponStats;
            if (quickMode)
            {
                activeWindow = new GunsmithWindowFrame(new RectTransform(new Vector2(0.78f, 0.68f), GUI.Canvas, Anchor.Center), CloseWindow, Color.Black * 0.62f);
                BuildQuickOverlay(title);
            }
            else
            {
                activeWindow = new GunsmithWindowFrame(new RectTransform(new Vector2(0.74f, 0.62f), GUI.Canvas, Anchor.Center), CloseWindow, Color.Black * 0.85f);
                BuildHeader(title);
                BuildBody();
            }

            try
            {
                activeWindow.AddToGUIUpdateList();
            }
            catch (Exception ex)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to add window to GUI update list: {ex.Message}");
            }
        }

        public static void RefreshPartsFromLua(Item item, string slotSpec)
            => RefreshInternal(item, slotSpec, quickMode: false);

        public static void RefreshQuickFromLua(Item item, string slotSpec)
            => RefreshInternal(item, slotSpec, quickMode: true);

        private static void RefreshInternal(Item item, string slotSpec, bool quickMode)
        {
            if (item == null || item.Removed || activeWindow == null || !ReferenceEquals(activeItem, item)) { return; }

            GunsmithGuiSpec spec = ParseSpec(slotSpec);
            if (spec.Slots.Count == 0) { return; }

            activeQuickMode = quickMode;
            string previousPath = activeContext.CurrentPath;
            activeContext = spec.Context;
            activeSlots = spec.Slots;
            activeWeaponStats = spec.WeaponStats;
            activePreviewSettings = spec.PreviewSettings;
            if (quickMode)
            {
                RebuildQuickOverlay(Key("ui.quick_title"));
                return;
            }

            SelectSlotAfterRefresh(previousPath, activeContext.CurrentPath);

            GunsmithGuiSlot? slot = activeSlots.FirstOrDefault(slot => slot.Path == selectedSlot) ?? activeSlots.FirstOrDefault();
            if (slot == null) { return; }

            selectedSlot = slot.Path;
            SelectDefaultPart(slot);
            RefreshPartList(slot);
            RefreshPartDetailPanel(slot);
        }

        private static void BuildHeader(string title)
        {
            if (activeWindow == null) { return; }

            GUIFrame header = new(new RectTransform(new Vector2(0.96f, 0.12f), activeWindow.RectTransform, Anchor.TopCenter), color: Color.Black * 0.35f);
            _ = new GUITextBlock(new RectTransform(new Vector2(0.76f, 0.78f), header.RectTransform, Anchor.CenterLeft), FormatL(title, LocalizedItemName(activeItem)), textAlignment: Alignment.CenterLeft);

            GUIButton closeButton = new(new RectTransform(new Vector2(0.16f, 0.72f), header.RectTransform, Anchor.CenterRight), L(Key("ui.close")), Alignment.Center);
            closeButton.OnClicked = (_, _) =>
            {
                CloseWindow();
                return true;
            };
        }

        private static void BuildBody()
        {
            if (activeWindow == null) { return; }

            GUIFrame body = new(new RectTransform(new Vector2(0.96f, 0.82f), activeWindow.RectTransform, Anchor.BottomCenter), color: Color.Transparent);
            BuildSlotPanel(body);

            GUIFrame middle = new(new RectTransform(new Vector2(0.36f, 0.96f), body.RectTransform, Anchor.Center), color: Color.Transparent);
            previewPanel = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.56f), middle.RectTransform, Anchor.TopCenter), color: Color.Black * 0.25f);
            detailPanel = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.40f), middle.RectTransform, Anchor.BottomCenter), color: Color.Black * 0.25f);
            GUIFrame rightPanel = new(new RectTransform(new Vector2(0.28f, 0.96f), body.RectTransform, Anchor.CenterRight), color: Color.Transparent);
            partList = new GUIListBox(new RectTransform(new Vector2(1.0f, 0.54f), rightPanel.RectTransform, Anchor.TopCenter), style: null)
            {
                PlaySoundOnSelect = true
            };
            partDetailPanel = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.42f), rightPanel.RectTransform, Anchor.BottomCenter), color: Color.Black * 0.25f);

            RefreshSelectionPanels();
        }

        private static void BuildQuickOverlay(string title)
        {
            if (activeWindow == null) { return; }
            quickOverlayFrame?.RestoreBuffersToWeapon();
            activeWindow.ClearChildren();

            GUIFrame header = new(new RectTransform(new Vector2(0.48f, 0.09f), activeWindow.RectTransform, Anchor.TopCenter), color: Color.Black * 0.35f);
            _ = new GUITextBlock(new RectTransform(new Vector2(0.72f, 0.72f), header.RectTransform, Anchor.CenterLeft), FormatL(title, LocalizedItemName(activeItem)), textAlignment: Alignment.CenterLeft);
            GUIButton closeButton = new(new RectTransform(new Vector2(0.20f, 0.72f), header.RectTransform, Anchor.CenterRight), L(Key("ui.close")), Alignment.Center);
            closeButton.OnClicked = (_, _) =>
            {
                CloseWindow();
                return true;
            };

            quickOverlayFrame = new QuickOverlayFrame(
                new RectTransform(new Vector2(0.92f, 0.78f), activeWindow.RectTransform, Anchor.Center),
                activeItem,
                activePreviewSettings,
                activeSlots);
        }

        private static void RebuildQuickOverlay(string title)
        {
            if (!activeQuickMode || activeWindow == null) { return; }
            BuildQuickOverlay(title);
            activeWindow.AddToGUIUpdateList();
        }

        private static void BuildSlotPanel(GUIFrame body)
        {
            slotList = new GUIListBox(new RectTransform(new Vector2(0.28f, 0.96f), body.RectTransform, Anchor.CenterLeft), style: null)
            {
                PlaySoundOnSelect = true
            };
            RebuildSlotList();
        }

        private static void RebuildSlotList()
        {
            if (slotList == null) { return; }
            slotList.Content.ClearChildren();

            _ = CreateListTitle(slotList, L(Key("ui.current_slots")));

            if (!string.IsNullOrWhiteSpace(activeContext.CurrentPath))
            {
                GUIFrame backRow = CreateListRow(slotList, L(Key("ui.back")).Value, Color.Cyan, null);
                backRow.UserData = "__back";
            }

            slotList.OnSelected = (_, userData) =>
            {
                if (userData is string back && back == "__back")
                {
                    if (activeItem != null && !activeItem.Removed)
                    {
                        GunsmithApi.CallLuaHook("GunsmithFrameworkEnterPath", activeItem, activeContext.ParentPath);
                    }
                    return true;
                }

                if (userData is not GunsmithGuiSlot slot) { return false; }
                selectedSlot = slot.Path;
                RebuildSlotList();
                RefreshSelectionPanels();
                return true;
            };

            foreach (GunsmithGuiSlot slot in activeSlots)
            {
                string label = LocalizeKey(slot.NameKey);
                GUIFrame row = CreateListRow(slotList, label, slot.Path == selectedSlot ? Color.LightGreen : Color.Cyan, slot);
                row.Color = slot.Path == selectedSlot ? Color.DarkOliveGreen * 0.55f : Color.Transparent;
            }
        }

        private static GUITextBlock CreateListTitle(GUIListBox list, LocalizedString text)
            => new(new RectTransform(new Point(list.Content.Rect.Width, (int)(40 * GUI.yScale)), list.Content.RectTransform), text, textAlignment: Alignment.Center);

        private static GUIFrame CreateListRow(GUIListBox list, string labelText, Color indicatorColor, object? userData)
        {
            GUIFrame frame = new(new RectTransform(new Point(list.Content.Rect.Width, (int)(40 * GUI.yScale)), list.Content.RectTransform), style: null)
            {
                UserData = userData,
                HoverColor = Color.Gold * 0.2f,
                SelectedColor = Color.Gold * 0.5f
            };
            _ = new GUITextBlock(new RectTransform(new Vector2(0.92f, 1.0f), frame.RectTransform, Anchor.Center), (LocalizedString)labelText, font: GUIStyle.SmallFont, textAlignment: Alignment.CenterLeft)
            {
                Padding = Vector4.Zero,
                AutoScaleVertical = true,
                CanBeFocused = false
            };
            return frame;
        }

        private static void RefreshSelectionPanels()
        {
            GunsmithGuiSlot? slot = activeSlots.FirstOrDefault(slot => slot.Path == selectedSlot) ?? activeSlots.FirstOrDefault();
            if (slot == null) { return; }
            selectedSlot = slot.Path;
            SelectDefaultPart(slot);

            RebuildPreviewPanel(slot);
            RebuildPartList(slot);
            RebuildPartDetailPanel(slot);
        }

        private static void SelectSlotAfterRefresh(string previousPath, string currentPath)
        {
            if (!string.IsNullOrWhiteSpace(previousPath) &&
                !string.Equals(previousPath, currentPath, StringComparison.Ordinal) &&
                (string.Equals(ParentPath(previousPath), currentPath ?? string.Empty, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(currentPath)) &&
                activeSlots.Any(slot => slot.Path == previousPath))
            {
                selectedSlot = previousPath;
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedSlot) || activeSlots.All(slot => slot.Path != selectedSlot))
            {
                selectedSlot = activeSlots.FirstOrDefault()?.Path;
                selectedPartId = null;
            }
        }

        private static string ParentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) { return string.Empty; }
            int slashIndex = path.LastIndexOf('/');
            return slashIndex > 0 ? path[..slashIndex] : string.Empty;
        }

        private static void RebuildPreviewPanel(GunsmithGuiSlot slot)
        {
            if (previewPanel == null) { return; }
            previewPanel.ClearChildren();
            detailPanel?.ClearChildren();

            if (activeItem != null && GunsmithApi.TryGetValidState(activeItem, out GunsmithSpriteState state))
            {
                _ = new GunsmithPreviewImage(
                    new RectTransform(new Vector2(0.92f, 0.86f), previewPanel.RectTransform, Anchor.Center),
                    activeItem,
                    activePreviewSettings);
            }
            else
            {
                _ = new GUITextBlock(new RectTransform(new Vector2(0.92f, 0.28f), previewPanel.RectTransform, Anchor.Center), L(Key("ui.preview_placeholder")), textAlignment: Alignment.Center);
            }

            if (detailPanel == null) { return; }
            _ = new GunsmithStatsText(new RectTransform(new Vector2(0.92f, 0.52f), detailPanel.RectTransform, Anchor.TopCenter), activeWeaponStats, inline: true);
            GUITextBlock pathText = new(new RectTransform(new Vector2(0.92f, 0.18f), detailPanel.RectTransform, Anchor.Center), FormatL(Key("ui.path_line"), LocalizePathLabel(activeContext.PathLabel)), font: GUIStyle.SmallFont, textAlignment: Alignment.Center)
            {
                AutoScaleVertical = true
            };
            pathText.RectTransform.RelativeOffset = new Vector2(0.0f, slot.CanEnter ? 0.18f : 0.28f);

            if (!slot.CanEnter) { return; }
            GUIButton enterButton = new(new RectTransform(new Vector2(0.72f, 0.20f), detailPanel.RectTransform, Anchor.BottomCenter), L(Key("ui.enter_mounts")), Alignment.Center);
            enterButton.OnClicked = (_, _) =>
            {
                if (activeItem != null && !activeItem.Removed)
                {
                    GunsmithApi.CallLuaHook("GunsmithFrameworkEnterPath", activeItem, slot.Path);
                }
                return true;
            };
        }

        private static void RebuildPartList(GunsmithGuiSlot slot)
        {
            if (partList == null) { return; }
            partList.Content.ClearChildren();
            partRows.Clear();
            partListSlotPath = slot.Path;
            SetPartListSelectionHandler(slot.Path);

            partListTitle = CreateListTitle(partList, FormatL(Key("ui.part_list_title"), LocalizeKey(slot.NameKey)));

            foreach (GunsmithGuiPart part in slot.Parts)
            {
                GunsmithPartRow row = CreatePartRow(slot, part);
                partRows[part.Id] = row;
                UpdatePartRow(slot, part, row);
            }
            GunsmithGuiPart? selectedPart = slot.Parts.FirstOrDefault(part => part.Id == selectedPartId);
            if (selectedPart != null)
            {
                partList.Select(selectedPart);
            }
        }

        private static GunsmithPartRow CreatePartRow(GunsmithGuiSlot slot, GunsmithGuiPart part)
        {
            GUIFrame frame = new(new RectTransform(new Point(partList!.Content.Rect.Width, (int)(40 * GUI.yScale)), partList.Content.RectTransform), style: null)
            {
                UserData = part,
                HoverColor = Color.Gold * 0.2f,
                SelectedColor = Color.Gold * 0.5f
            };
            AddPartPrefabImage(new RectTransform(new Point(frame.Rect.Height, frame.Rect.Height), frame.RectTransform, Anchor.CenterLeft), part, 0.82f);
            GUITextBlock label = new(new RectTransform(new Vector2(0.70f, 1.0f), frame.RectTransform, Anchor.CenterLeft), (LocalizedString)LocalizeKey(part.NameKey), font: GUIStyle.SmallFont, textAlignment: Alignment.CenterLeft)
            {
                Padding = new Vector4(frame.Rect.Height + (int)(6 * GUI.xScale), 0, 0, 0),
                AutoScaleVertical = true,
                CanBeFocused = false
            };
            GUITextBlock status = new(new RectTransform(new Vector2(0.26f, 1.0f), frame.RectTransform, Anchor.CenterRight), (LocalizedString)PartStatusText(slot, part), font: GUIStyle.SmallFont, textAlignment: Alignment.CenterRight)
            {
                Padding = Vector4.Zero,
                AutoScaleVertical = true,
                CanBeFocused = false
            };
            return new GunsmithPartRow(frame, label, status);
        }

        private static void RefreshPartList(GunsmithGuiSlot slot)
        {
            if (partList == null) { return; }
            if (!CanUpdatePartList(slot))
            {
                RebuildPartList(slot);
                return;
            }

            SetPartListSelectionHandler(slot.Path);
            if (partListTitle != null)
            {
                partListTitle.Text = FormatL(Key("ui.part_list_title"), LocalizeKey(slot.NameKey));
            }

            foreach (GunsmithGuiPart part in slot.Parts)
            {
                if (partRows.TryGetValue(part.Id, out GunsmithPartRow? row))
                {
                    UpdatePartRow(slot, part, row);
                }
            }
        }

        private static bool CanUpdatePartList(GunsmithGuiSlot slot)
            => string.Equals(partListSlotPath, slot.Path, StringComparison.Ordinal) &&
               partListTitle != null &&
               partRows.Count == slot.Parts.Count &&
               slot.Parts.All(part => partRows.ContainsKey(part.Id));

        private static void UpdatePartRow(GunsmithGuiSlot slot, GunsmithGuiPart part, GunsmithPartRow row)
        {
            bool selected = part.Id == selectedPartId;
            row.Frame.UserData = part;
            row.Frame.Color = selected ? Color.DarkOliveGreen * 0.55f : Color.Transparent;
            row.Label.Text = (LocalizedString)LocalizeKey(part.NameKey);
            row.Status.Text = (LocalizedString)PartStatusText(slot, part);
            row.Status.TextColor = PartStatusColor(slot, part);
        }

        private static void SetPartListSelectionHandler(string slotPath)
        {
            if (partList == null) { return; }
            partList.OnSelected = (_, userData) =>
            {
                if (userData is not GunsmithGuiPart part) { return false; }
                GunsmithGuiSlot? latestSlot = activeSlots.FirstOrDefault(slot => slot.Path == slotPath);
                if (latestSlot == null) { return false; }

                selectedSlot = latestSlot.Path;
                selectedPartId = part.Id;
                RefreshPartList(latestSlot);
                RebuildPartDetailPanel(latestSlot);
                return true;
            };
        }

        private static bool IsPartInstalled(GunsmithGuiSlot slot, GunsmithGuiPart part)
            => part.Id == slot.CurrentPartId || (part.Id == EmptyPartId && string.IsNullOrWhiteSpace(slot.CurrentPartId));

        private static string PartStatusText(GunsmithGuiSlot slot, GunsmithGuiPart part)
        {
            string key = IsPartInstalled(slot, part)
                ? Key("status.installed")
                : part.Status switch
                {
                    "missing" => Key("status.missing"),
                    "incompatible" => Key("status.incompatible"),
                    "disabled" => Key("status.disabled"),
                    _ => string.Empty
                };
            if (string.IsNullOrWhiteSpace(key)) { return string.Empty; }
            return FormatLValue(key, string.Empty).Trim();
        }

        private static Color PartStatusColor(GunsmithGuiSlot slot, GunsmithGuiPart part)
        {
            if (IsPartInstalled(slot, part)) { return Color.LightGreen; }
            return part.Status switch
            {
                "missing" => Color.DarkRed,
                "incompatible" => Color.OrangeRed,
                "disabled" => Color.Gray,
                _ => Color.Cyan
            };
        }

        private static void RebuildPartDetailPanel(GunsmithGuiSlot slot)
        {
            if (partDetailPanel == null) { return; }
            partDetailPanel.ClearChildren();
            partDetailSlotPath = slot.Path;

            GunsmithGuiPart? part = slot.Parts.FirstOrDefault(part => part.Id == selectedPartId) ?? slot.Parts.FirstOrDefault();
            if (part == null) { return; }
            selectedPartId = part.Id;

            bool installed = part.Id == slot.CurrentPartId || (part.Id == EmptyPartId && string.IsNullOrWhiteSpace(slot.CurrentPartId));
            // 右下详情面板标题：“选中配件属性”。第一个 Vector2 控制标题占面板宽高，Anchor.TopCenter 固定在面板顶部居中。
            _ = new GUITextBlock(new RectTransform(new Vector2(0.96f, 0.12f), partDetailPanel.RectTransform, Anchor.TopCenter), L(Key("ui.part_detail_title")), textAlignment: Alignment.Center);

            // 右下详情面板中部内容容器：左侧预览图 + 右侧名称/属性。调这里可整体移动或缩放中部内容。
            // Vector2.X 控制宽度比例，Vector2.Y 控制高度比例；Anchor.Center 表示内容块以详情面板中心为基准。
            // RelativeOffset.Y 控制整个“预览图 + 属性文字”上下位置：负数往上，正数往下。
            GUIFrame content = new(new RectTransform(new Vector2(0.94f, 0.42f), partDetailPanel.RectTransform, Anchor.Center), style: null, color: Color.Transparent)
            {
                RectTransform = { RelativeOffset = new Vector2(0.0f, -0.14f) }
            };

            // 左侧预览图区域。Vector2.X 是左栏宽度比例；Anchor.CenterLeft 表示贴在 content 左侧并垂直居中。
            GUIFrame left = new(new RectTransform(new Vector2(0.34f, 1.0f), content.RectTransform, Anchor.CenterLeft), style: null, color: Color.Transparent);

            // 预览图正方形边长。0.78f 越大，预览框越大；48 是最小像素尺寸。
            int imageSize = Math.Max((int)Math.Round(Math.Min(left.Rect.Width, left.Rect.Height) * 0.78f), 48);

            // 配件预览框。Anchor.Center 表示预览框在左栏中居中；OutlineColor/OutlineThickness 控制边框颜色和粗细。
            GUIFrame imageFrame = new(new RectTransform(new Point(imageSize, imageSize), left.RectTransform, Anchor.Center), color: Color.Black * 0.35f)
            {
                OutlineColor = GUIStyle.Green * 0.85f,
                OutlineThickness = 2
            };
            // 预览框内部图片。Vector2 控制图片在边框内可用空间比例，fill 控制图片最终填充比例。
            AddPartPrefabImage(new RectTransform(new Vector2(0.88f, 0.88f), imageFrame.RectTransform, Anchor.Center), part, 0.92f);

            // 右侧文字区域：配件名称 + 属性。Vector2.X 是右栏宽度比例；Anchor.CenterRight 表示贴在 content 右侧。
            GUIFrame right = new(new RectTransform(new Vector2(0.62f, 1.0f), content.RectTransform, Anchor.CenterRight), style: null, color: Color.Transparent);

            // 配件名称文本。Vector2.Y 控制名称区域高度；Anchor.TopLeft 表示从右侧文字区域左上角开始。
            _ = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.22f), right.RectTransform, Anchor.TopLeft), (LocalizedString)LocalizeKey(part.NameKey), font: GUIStyle.SmallFont, textAlignment: Alignment.TopLeft)
            {
                TextColor = Color.Cyan,
                Padding = Vector4.Zero,
                Wrap = true
            };

            // 属性文本。Vector2.Y 控制属性区域高度；Anchor.BottomLeft 表示贴在右侧文字区域左下方。
            // 如果属性太靠下，可改 Anchor.CenterLeft 或增大/减小此处高度比例。
            _ = new GunsmithStatsText(new RectTransform(new Vector2(1.0f, 0.76f), right.RectTransform, Anchor.BottomLeft), part.Stats, inline: false);
            string buttonText = InstallButtonText(part, installed);

            // 底部安装按钮。Vector2 控制按钮宽高比例；Anchor.BottomCenter 表示固定在详情面板底部居中。
            partDetailActionButton = new GUIButton(new RectTransform(new Vector2(0.72f, 0.18f), partDetailPanel.RectTransform, Anchor.BottomCenter), (LocalizedString)buttonText, Alignment.Center);
            UpdatePartDetailAction(slot, part, installed);
        }

        private static void RefreshPartDetailPanel(GunsmithGuiSlot slot)
        {
            if (partDetailPanel == null) { return; }
            if (!string.Equals(partDetailSlotPath, slot.Path, StringComparison.Ordinal) ||
                partDetailActionButton == null)
            {
                RebuildPartDetailPanel(slot);
                return;
            }

            GunsmithGuiPart? part = slot.Parts.FirstOrDefault(part => part.Id == selectedPartId) ?? slot.Parts.FirstOrDefault();
            if (part == null)
            {
                RebuildPartDetailPanel(slot);
                return;
            }

            selectedPartId = part.Id;
            bool installed = part.Id == slot.CurrentPartId || (part.Id == EmptyPartId && string.IsNullOrWhiteSpace(slot.CurrentPartId));
            UpdatePartDetailAction(slot, part, installed);
        }

        private static void UpdatePartDetailAction(GunsmithGuiSlot slot, GunsmithGuiPart part, bool installed)
        {
            if (partDetailActionButton == null) { return; }
            partDetailActionButton.Text = (LocalizedString)InstallButtonText(part, installed);
            partDetailActionButton.Enabled = !installed && part.IsActionable;
            partDetailActionButton.OnClicked = (_, _) =>
            {
                if (!installed && part.IsActionable && activeItem != null && !activeItem.Removed)
                {
                    GunsmithApi.CallLuaHook(activeQuickMode ? "GunsmithFrameworkSetQuickPart" : "GunsmithFrameworkSetPart", activeItem, slot.Path, part.Id);
                }
                return true;
            };
        }

        private static void SelectDefaultPart(GunsmithGuiSlot slot)
        {
            if (slot.Parts.Any(part => part.Id == selectedPartId)) { return; }
            selectedPartId = slot.Parts.FirstOrDefault(part => part.Id == slot.CurrentPartId)?.Id;
            if (string.IsNullOrWhiteSpace(selectedPartId) && string.IsNullOrWhiteSpace(slot.CurrentPartId))
            {
                selectedPartId = slot.Parts.FirstOrDefault(part => part.Id == EmptyPartId)?.Id;
            }
            selectedPartId ??= slot.Parts.FirstOrDefault()?.Id;
        }



        internal static void CloseWindow()
        {
            if (activeWindow == null) { return; }
            quickOverlayFrame?.RestoreBuffersToWeapon();
            activeWindow.RectTransform.Parent = null;
            activeWindow = null;
            activeItem = null;
            slotList = null;
            previewPanel = null;
            detailPanel = null;
            partList = null;
            partDetailPanel = null;
            partListTitle = null;
            partRows.Clear();
            partDetailActionButton = null;
            partListSlotPath = null;
            partDetailSlotPath = null;
            selectedPartId = null;
            activePreviewSettings = GunsmithPreviewSettings.Default;
            activeWeaponStats = GunsmithStats.Empty;
            activeQuickMode = false;
            quickOverlayFrame = null;
            suppressQuickUninstallRelease = false;
            pendingQuickDrag = null;
            pendingNativeQuickDragDropClearItem = null;
            warnedQuickAnchorPaths.Clear();
        }

        internal static bool IsGunsmithWindowBlockingInput
            => activeWindow is { Visible: true };

        internal static bool IsOpenForItem(Item item, bool quickMode)
            => item != null &&
               activeWindow is { Visible: true } &&
               ReferenceEquals(activeItem, item) &&
               activeQuickMode == quickMode;

        internal static GUIComponent? ActiveWindowForInputBlock => activeWindow;

        internal static bool IsMouseOnQuickBufferInventory
            => activeQuickMode &&
               activeWindow is { Visible: true } &&
               quickOverlayFrame?.IsMouseOnBufferInventory() == true;

        internal static bool TryHandleQuickOverlayDragging()
        {
            if (!activeQuickMode || quickOverlayFrame == null)
            {
                return false;
            }

            return quickOverlayFrame.TryHandleDraggingRelease();
        }

        internal static bool TryHandlePendingQuickDragNativeSlotDrop(
            Inventory targetInventory,
            Item draggedItem,
            int targetSlotIndex,
            bool allowSwapping,
            bool allowCombine,
            Character user,
            bool createNetworkEvent,
            bool ignoreCondition,
            bool triggerOnInsertedEffects,
            ref bool result)
            => QuickOverlayFrame.TryHandlePendingQuickDragNativeSlotDrop(
                targetInventory,
                draggedItem,
                targetSlotIndex,
                allowSwapping,
                allowCombine,
                user,
                createNetworkEvent,
                ignoreCondition,
                triggerOnInsertedEffects,
                ref result);

        internal static void ReconcilePendingQuickDragAfterNativeDragging()
            => QuickOverlayFrame.ReconcilePendingQuickDragAfterNativeDragging();

        internal static void RefreshWindow()
        {
            if (activeWindow == null) { return; }
            try
            {
                activeWindow.AddToGUIUpdateList();
            }
            catch (Exception ex)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to refresh window: {ex.Message}");
                CloseWindow();
            }
        }





    }
}
