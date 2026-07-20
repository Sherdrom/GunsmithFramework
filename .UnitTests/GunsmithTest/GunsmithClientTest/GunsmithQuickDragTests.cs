using System.Reflection;
using System.Runtime.CompilerServices;
using Barotrauma;
using GunsmithFramework;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace GunsmithClientTest;

public sealed class GunsmithQuickDragTests : IDisposable
{
    public GunsmithQuickDragTests() => ClearState();

    public void Dispose() => ClearState();

    [Fact]
    public void ResetWithoutPendingDragIsSafeAndIdempotent()
    {
        GunsmithQuickDrag.Reset();
        GunsmithQuickDrag.Reset();

        Assert.Null(GetStaticField("pendingQuickDrag"));
        Assert.Null(GetStaticField("pendingNativeQuickDragDropClearItem"));
        Assert.False((bool)GetStaticField("handlingNativeQuickDragDrop")!);
    }

    [Fact]
    public void PendingDragMatchesOnlyItsExactSource()
    {
        Item weapon = Uninitialized<Item>();
        Item dragged = Uninitialized<Item>();
        SetPending(weapon, "receiver/optic", 3, dragged);

        Assert.True(GunsmithQuickDrag.MatchesPendingSource(weapon, dragged, "receiver/optic", 3));
        Assert.False(GunsmithQuickDrag.MatchesPendingSource(Uninitialized<Item>(), dragged, "receiver/optic", 3));
        Assert.False(GunsmithQuickDrag.MatchesPendingSource(weapon, Uninitialized<Item>(), "receiver/optic", 3));
        Assert.False(GunsmithQuickDrag.MatchesPendingSource(weapon, dragged, "receiver/rail", 3));
        Assert.False(GunsmithQuickDrag.MatchesPendingSource(weapon, dragged, "receiver/optic", 4));
    }

    [Fact]
    public void UnrelatedItemAndInvalidTargetAreNotIntercepted()
    {
        Item weapon = Uninitialized<Item>();
        Item dragged = Uninitialized<Item>();
        SetPending(weapon, "receiver/optic", 0, dragged);
        Inventory target = CreateInventory(1);
        bool result = true;

        Assert.False(GunsmithQuickDrag.TryHandleNativeSlotDrop(
            target, Uninitialized<Item>(), 0, true, true, null!, false, false, false, ref result));
        Assert.False(result);

        result = true;
        Assert.False(GunsmithQuickDrag.TryHandleNativeSlotDrop(
            target, dragged, 1, true, true, null!, false, false, false, ref result));
        Assert.False(result);
    }

    [Fact]
    public void DropIntoSourceWeaponInventoryIsLeftToExistingPath()
    {
        Item weapon = Uninitialized<Item>();
        Item dragged = Uninitialized<Item>();
        ItemInventory source = CreateInventory(1);
        SetInstanceField(weapon, "ownInventory", source);
        SetPending(weapon, "receiver/optic", 0, dragged);
        bool result = true;

        Assert.False(GunsmithQuickDrag.TryHandleNativeSlotDrop(
            source, dragged, 0, true, true, null!, false, false, false, ref result));
        Assert.False(result);
    }

    [Fact]
    public void AllowedNativeCombineIsLeftToNativeInventory()
    {
        Assert.True(GunsmithQuickDrag.CanNativeInventoryCombine(true, "ammo", "AMMO"));
        Assert.False(GunsmithQuickDrag.CanNativeInventoryCombine(false, "ammo", "ammo"));
        Assert.False(GunsmithQuickDrag.CanNativeInventoryCombine(true, "", "ammo"));
        Assert.False(GunsmithQuickDrag.CanNativeInventoryCombine(true, "ammo", "shell"));
    }

    [Fact]
    public void RemovedWeaponCancelsPendingWithoutLosingDraggedItem()
    {
        Item weapon = Uninitialized<Item>();
        Item dragged = Uninitialized<Item>();
        SetRemoved(weapon);
        SetPending(weapon, "receiver/optic", 0, dragged);
        Inventory.DraggingItems.Add(dragged);

        Assert.False(GunsmithQuickDrag.ReconcileAfterNativeDragging());
        Assert.Null(GetStaticField("pendingQuickDrag"));
        Assert.Contains(dragged, Inventory.DraggingItems);
    }

    [Fact]
    public void SuccessfulReconciliationClearsPendingAndDelayedState()
    {
        Item weapon = Uninitialized<Item>();
        Item dragged = Uninitialized<Item>();
        Item delayedClear = Uninitialized<Item>();
        SetInstanceField(dragged, "parentInventory", CreateInventory(1));
        SetPending(weapon, "receiver/optic", 0, dragged);
        SetStaticField("pendingNativeQuickDragDropClearItem", delayedClear);
        Inventory.DraggingItems.Add(delayedClear);

        Assert.False(GunsmithQuickDrag.ReconcileAfterNativeDragging());
        Assert.Null(GetStaticField("pendingQuickDrag"));
        Assert.Null(GetStaticField("pendingNativeQuickDragDropClearItem"));
        Assert.DoesNotContain(delayedClear, Inventory.DraggingItems);
    }

    private static ItemInventory CreateInventory(int slotCount)
    {
        ItemInventory inventory = Uninitialized<ItemInventory>();
        FieldInfo slotsField = FindInstanceField(typeof(Inventory), "slots");
        Type slotType = slotsField.FieldType.GetElementType()!;
        Array slots = Array.CreateInstance(slotType, slotCount);
        for (int i = 0; i < slotCount; i++)
        {
            slots.SetValue(Activator.CreateInstance(
                slotType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { inventory },
                culture: null), i);
        }
        slotsField.SetValue(inventory, slots);
        FindInstanceField(typeof(Inventory), "capacity").SetValue(inventory, slotCount);
        return inventory;
    }

    private static void SetPending(Item weapon, string slotPath, int slotIndex, Item dragged)
    {
        Type pendingType = typeof(GunsmithQuickDrag).GetNestedType("PendingQuickDrag", BindingFlags.NonPublic)!;
        object pending = Activator.CreateInstance(
            pendingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { weapon, slotPath, slotIndex, dragged },
            culture: null)!;
        SetStaticField("pendingQuickDrag", pending);
    }

    private static void SetRemoved(Item item)
        => FindInstanceField(typeof(Item), "<Removed>k__BackingField").SetValue(item, true);

    private static void SetInstanceField(object instance, string fieldName, object? value)
        => FindInstanceField(instance.GetType(), fieldName).SetValue(instance, value);

    private static FieldInfo FindInstanceField(Type type, string fieldName)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            FieldInfo? field = current.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return field;
            }
        }
        throw new MissingFieldException(type.FullName, fieldName);
    }

    private static object? GetStaticField(string fieldName)
        => typeof(GunsmithQuickDrag).GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null);

    private static void SetStaticField(string fieldName, object? value)
        => typeof(GunsmithQuickDrag).GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(null, value);

    private static void ClearState()
    {
        SetStaticField("pendingQuickDrag", null);
        SetStaticField("pendingNativeQuickDragDropClearItem", null);
        SetStaticField("handlingNativeQuickDragDrop", false);
        Inventory.DraggingItems.Clear();
        Inventory.DraggingSlot = null;
    }

    private static T Uninitialized<T>() where T : class
        => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
