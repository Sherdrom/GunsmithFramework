namespace GunsmithFramework
{
    internal static class GunsmithQuickTransformMath
    {
        public static bool TryItemLocalToWorldPosition(Item item, Vector2 itemLocalDisplayPos, bool drawPosition, out Vector2 position)
        {
            position = Vector2.Zero;
            if (item == null || item.Removed || !IsFinite(itemLocalDisplayPos) || !float.IsFinite(item.Scale))
            {
                return false;
            }

            Vector2 localOffset = itemLocalDisplayPos * item.Scale;
            float rotation;
            Vector2 bodyPosition;
            if (item.body != null)
            {
                if (item.body.Dir < 0.0f)
                {
                    localOffset.X = -localOffset.X;
                }

                rotation = drawPosition ? item.body.DrawRotation : item.body.Rotation;
                bodyPosition = drawPosition ? item.body.DrawPosition : item.body.Position;
            }
            else
            {
                rotation = item.RotationRad;
                bodyPosition = drawPosition ? item.DrawPosition : item.Position;
            }

            float sin = MathF.Sin(rotation);
            float cos = MathF.Cos(rotation);
            position = new Vector2(
                bodyPosition.X + localOffset.X * cos - localOffset.Y * sin,
                bodyPosition.Y + localOffset.X * sin + localOffset.Y * cos);

            return IsFinite(position);
        }

        public static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
