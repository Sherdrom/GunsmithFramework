using System.Reflection;
using System.Runtime.CompilerServices;
using Barotrauma;
using GunsmithFramework;
using Microsoft.Xna.Framework;
using Xunit;

namespace GunsmithClientTest;

public sealed class GunsmithReloadCleanupTests
{
    [Fact]
    public void Reset_RestoresTrackedVisualSlotLayouts()
    {
        VisualSlot first = new(new Rectangle(10, 20, 30, 40))
        {
            InteractRect = new Rectangle(11, 21, 28, 38),
            SubInventoryDir = -1
        };
        VisualSlot second = new(new Rectangle(50, 60, 30, 40))
        {
            InteractRect = new Rectangle(51, 61, 28, 38),
            SubInventoryDir = 1
        };
        Inventory inventory = (Inventory)RuntimeHelpers.GetUninitializedObject(typeof(Inventory));
        inventory.visualSlots = new[] { first, second };

        MethodInfo capture = typeof(GunsmithHiddenQuickSlotsPatch).GetMethod(
            "CaptureOriginalLayouts",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        Type ownerPlacementType = capture.GetParameters()[1].ParameterType;
        capture.Invoke(null, new[] { inventory, Activator.CreateInstance(ownerPlacementType)! });

        first.Rect = new Rectangle(100, 200, 1, 1);
        first.InteractRect = new Rectangle(101, 201, 1, 1);
        first.SubInventoryDir = 0;
        second.Rect = first.Rect;
        second.InteractRect = first.InteractRect;
        second.SubInventoryDir = 0;

        GunsmithHiddenQuickSlotsPatch.Reset();

        Assert.Equal(new Rectangle(10, 20, 30, 40), first.Rect);
        Assert.Equal(new Rectangle(11, 21, 28, 38), first.InteractRect);
        Assert.Equal(-1, first.SubInventoryDir);
        Assert.Equal(new Rectangle(50, 60, 30, 40), second.Rect);
        Assert.Equal(new Rectangle(51, 61, 28, 38), second.InteractRect);
        Assert.Equal(1, second.SubInventoryDir);
    }
}
