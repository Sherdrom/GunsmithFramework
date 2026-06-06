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
            private bool dragging;

            public GunsmithWindowFrame(RectTransform rectT, Action close, Color color)
                : base(rectT, style: null, color: color)
            {
                this.close = close;
                CanBeFocused = true;
            }

            public override void Update(float deltaTime)
            {
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

                Rectangle dragArea = new(Rect.X, Rect.Y, Rect.Width, Math.Max((int)(Rect.Height * 0.14f), 28));
                if (dragArea.Contains(PlayerInput.MousePosition) && PlayerInput.PrimaryMouseButtonDown())
                {
                    dragging = true;
                }

                if (!PlayerInput.PrimaryMouseButtonHeld())
                {
                    dragging = false;
                    return;
                }

                if (!dragging)
                {
                    return;
                }

                Vector2 speed = PlayerInput.MouseSpeed;
                if (speed == Vector2.Zero)
                {
                    return;
                }

                RectTransform.ScreenSpaceOffset += new Point((int)Math.Round(speed.X), (int)Math.Round(speed.Y));
                ClampToArea(new Rectangle(0, 0, GameMain.GraphicsWidth, GameMain.GraphicsHeight));
            }
        }
    }
}
