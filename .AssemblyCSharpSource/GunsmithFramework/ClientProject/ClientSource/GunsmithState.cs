namespace GunsmithFramework
{
    public sealed class GunsmithLayer
    {
        public string SlotPath { get; init; } = string.Empty;
        public string PartId { get; init; } = string.Empty;
        public string ItemIdentifier { get; init; } = string.Empty;
        public string TexturePath { get; init; } = string.Empty;
        public Rectangle SourceRect { get; init; }
        public Vector2 Offset { get; init; }
        public float Scale { get; init; } = 1.0f;
        public int Order { get; init; }

        public Rectangle DrawBounds
        {
            get
            {
                int x = (int)Math.Floor(Offset.X);
                int y = (int)Math.Floor(Offset.Y);
                int width = (int)Math.Ceiling(SourceRect.Width * Scale);
                int height = (int)Math.Ceiling(SourceRect.Height * Scale);
                return new Rectangle(x, y, Math.Max(width, 1), Math.Max(height, 1));
            }
        }
    }

    internal sealed class GunsmithSpriteState
    {
        public string Signature { get; init; } = string.Empty;
        public Texture2D Texture { get; init; } = null!;
        public Texture2D WorldTexture { get; init; } = null!;
        public Texture2D InventoryTexture { get; init; } = null!;
        public Sprite WorldSprite { get; init; } = null!;
        public Sprite InventorySprite { get; init; } = null!;
        public Rectangle ContentBounds { get; init; }
        public IReadOnlyList<GunsmithLayer> Layers { get; init; } = Array.Empty<GunsmithLayer>();
        public Vector2 CanvasOrigin { get; init; }
        public GunsmithWorldSettings WorldSettings { get; init; } = GunsmithWorldSettings.Default;
    }

    internal sealed record GunsmithInventorySettings(float Scale, float RotationDegrees, float Padding)
    {
        public static GunsmithInventorySettings Default { get; } = new(1.0f, 0.0f, 0.0f);
    }

    internal sealed record GunsmithWorldSettings(float Scale, float RotationDegrees, float Padding, Vector2 Offset)
    {
        public static GunsmithWorldSettings Default { get; } = new(1.0f, 0.0f, 0.0f, Vector2.Zero);
    }

}
