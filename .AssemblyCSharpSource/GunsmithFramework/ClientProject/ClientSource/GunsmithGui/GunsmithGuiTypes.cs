namespace GunsmithFramework
{
    public static partial class GunsmithGui
    {
        internal sealed record GunsmithGuiSpec(GunsmithGuiContext Context, GunsmithPreviewSettings PreviewSettings, GunsmithStats WeaponStats, List<GunsmithGuiSlot> Slots);

        internal sealed record GunsmithGuiContext(string CurrentPath, string PathLabel, string ParentPath)
        {
            public static GunsmithGuiContext Empty { get; } = new(string.Empty, DefaultLocalizationPrefix + ".ui.weapon_root", string.Empty);
        }

        internal sealed record GunsmithGuiSlot(string Path, string NameKey, string CurrentPartId, bool CanEnter, List<GunsmithGuiPart> Parts, GunsmithQuickSlotMeta QuickMeta);

        internal sealed record GunsmithQuickSlotMeta(int SlotIndex, Vector2 Anchor, bool AnchorValid, IReadOnlySet<string> AllowedItemIdentifiers)
        {
            public static GunsmithQuickSlotMeta Empty { get; } = new(-1, Vector2.Zero, false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private sealed record GunsmithPartRow(GUIFrame Frame, GUITextBlock Label, GUITextBlock Status);

        internal sealed record GunsmithGuiPart(string Id, string NameKey, string Status, GunsmithStats Stats, string ItemIdentifier, string VisualTexturePath, Rectangle VisualSourceRect)
        {
            public bool IsActionable => Status != "missing" && Status != "disabled" && Status != "incompatible";
        }

        internal sealed class GunsmithStats
        {
            public float Ergonomics { get; init; }
            public IReadOnlyDictionary<StatTypes, float> Values { get; init; } = new Dictionary<StatTypes, float>();

            public float Get(StatTypes statType)
                => Values.TryGetValue(statType, out float value) ? value : 0.0f;

            public static GunsmithStats Empty { get; } = new();
        }

        internal sealed record GunsmithPreviewSettings(float Padding, float Scale, Vector2 Offset)
        {
            public static GunsmithPreviewSettings Default { get; } = new(12.0f, 1.0f, Vector2.Zero);
        }

        private sealed class GunsmithWindowFrame : GUIFrame
        {
            private readonly Action close;
            private readonly GUIDragHandle dragHandle;

            public GunsmithWindowFrame(RectTransform rectT, Action close)
                : base(rectT, style: "ItemUI")
            {
                this.close = close;
                CanBeFocused = true;

                dragHandle = new GUIDragHandle(new RectTransform(new Vector2(1.0f, 0.0f), RectTransform, Anchor.TopCenter)
                {
                    MinSize = new Point(0, GUIStyle.ItemFrameMargin.Y / 2)
                }, RectTransform, style: null)
                {
                    DragArea = HUDLayoutSettings.ItemHUDArea
                };

                int iconHeight = GUIStyle.ItemFrameMargin.Y / 4;
                _ = new GUIImage(new RectTransform(new Point(Rect.Width, iconHeight), dragHandle.RectTransform, Anchor.TopCenter)
                {
                    AbsoluteOffset = new Point(0, iconHeight / 2),
                    MinSize = new Point(0, iconHeight)
                }, style: "GUIDragIndicatorHorizontal");

                int buttonHeight = (int)(GUIStyle.ItemFrameMargin.Y * 0.4f);
                _ = new GUIButton(new RectTransform(new Point(buttonHeight), dragHandle.RectTransform, Anchor.TopLeft)
                {
                    AbsoluteOffset = new Point(buttonHeight / 4),
                    MinSize = new Point(buttonHeight)
                }, style: "GUIButtonSettings")
                {
                    OnClicked = (_, _) =>
                    {
                        GUIContextMenu.CreateContextMenu(
                            new ContextMenuOption("item.resetuiposition", isEnabled: true, onSelected: () => RectTransform.ScreenSpaceOffset = Point.Zero),
                            new ContextMenuOption(TextManager.Get(dragHandle.Enabled ? "item.lockuiposition" : "item.unlockuiposition"), isEnabled: true, onSelected: () => dragHandle.Enabled = !dragHandle.Enabled));
                        return true;
                    }
                };
            }

            public override void ClearChildren()
            {
                foreach (GUIComponent child in Children.Where(child => child != dragHandle).ToList())
                {
                    child.RectTransform.Parent = null;
                }
            }

            public override void AddToGUIUpdateList(bool ignoreChildren = false, int order = 0)
            {
                if (GUI.InputBlockingMenuOpen) { return; }
                base.AddToGUIUpdateList(ignoreChildren, order: 0);
            }

            public override void Update(float deltaTime)
            {
                if (GUI.InputBlockingMenuOpen) { return; }
                base.Update(deltaTime);
                if (!Visible) { return; }

                bool mouseInsideWindow = Rect.Contains(PlayerInput.MousePosition);
                if (!mouseInsideWindow)
                {
                    GUI.ForceMouseOn(this);
                }

                if (PlayerInput.SecondaryMouseButtonClicked())
                {
                    close();
                    return;
                }
            }
        }
    }
}
