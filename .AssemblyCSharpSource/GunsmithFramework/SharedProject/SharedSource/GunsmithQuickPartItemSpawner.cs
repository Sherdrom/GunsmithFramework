using System.Runtime.CompilerServices;

namespace GunsmithFramework
{
    internal static class GunsmithQuickPartItemSpawner
    {
        private static readonly ConditionalWeakTable<Item, HashSet<string>> PendingQuickPartSpawns = new();
        private static int generation;

        internal static Action<Item>? BeginQuickSlotMutation { get; set; }
        internal static Action<Item>? EndQuickSlotMutation { get; set; }

        internal static void Reset()
        {
            generation++;
            PendingQuickPartSpawns.Clear();
            BeginQuickSlotMutation = null;
            EndQuickSlotMutation = null;
        }

        internal static bool Ensure(Item weaponItem, int slotIndex, string itemIdentifier, bool createNetworkEvent)
        {
            if (weaponItem.OwnInventory == null ||
                slotIndex < 0 ||
                slotIndex >= weaponItem.OwnInventory.slots.Length)
            {
                DebugConsole.NewMessage(
                    $"[GunsmithFramework] NPC preset quick-slot item '{itemIdentifier}' could not be inserted because '{weaponItem.Prefab.Identifier.Value}' slot {slotIndex} is unavailable.",
                    Color.Yellow,
                    false);
                return false;
            }

            Identifier identifier = itemIdentifier.ToIdentifier();
            Item? existing = weaponItem.OwnInventory.slots[slotIndex].FirstOrDefault();
            if (existing != null)
            {
                if (!existing.Removed && existing.Prefab.Identifier == identifier)
                {
                    return true;
                }
            }

            ItemPrefab? prefab = FindItemPrefab(itemIdentifier);
            if (prefab == null)
            {
                DebugConsole.ThrowError($"[GunsmithFramework] NPC preset quick-slot item prefab not found: {itemIdentifier}");
                return false;
            }

            if (Entity.Spawner == null || Entity.Spawner.Removed)
            {
                if (GameMain.NetworkMember != null) { return false; }

                Item spawned = new(prefab, weaponItem.WorldPosition, null);
                if (TryPutQuickPartItem(weaponItem, spawned, slotIndex, identifier, createNetworkEvent: false))
                {
                    return true;
                }

                DebugConsole.ThrowError(
                    $"[GunsmithFramework] Failed to put '{itemIdentifier}' into '{weaponItem.Prefab.Identifier.Value}' slot {slotIndex} during map loading.");
                spawned.Remove();
                return false;
            }

            HashSet<string> pendingSpawns = PendingQuickPartSpawns.GetOrCreateValue(weaponItem);
            string pendingKey = $"{slotIndex}:{identifier.Value}";
            if (!pendingSpawns.Add(pendingKey))
            {
                return true;
            }

            int queuedGeneration = generation;
            Entity.Spawner.AddItemToSpawnQueue(prefab, weaponItem.WorldPosition, null, null, spawned =>
            {
                try
                {
                    if (queuedGeneration != generation ||
                        spawned == null ||
                        weaponItem.Removed ||
                        weaponItem.OwnInventory == null)
                    {
                        RemoveSpawnedItem(spawned);
                        return;
                    }

                    bool inserted = TryPutQuickPartItem(weaponItem, spawned, slotIndex, identifier, createNetworkEvent);
                    if (!inserted)
                    {
                        DebugConsole.ThrowError(
                            $"[GunsmithFramework] Failed to put NPC preset item '{itemIdentifier}' into '{weaponItem.Prefab.Identifier.Value}' slot {slotIndex}.");
                        RemoveSpawnedItem(spawned);
                    }
                }
                finally
                {
                    if (queuedGeneration == generation)
                    {
                        pendingSpawns.Remove(pendingKey);
                    }
                }
            });

            return true;
        }

        private static bool TryPutQuickPartItem(Item weaponItem, Item spawned, int slotIndex, Identifier identifier, bool createNetworkEvent)
        {
            BeginQuickSlotMutation?.Invoke(weaponItem);
            try
            {
                RemoveMismatchedSlotItems(weaponItem, slotIndex, identifier);
                return weaponItem.OwnInventory?.TryPutItem(
                    spawned,
                    slotIndex,
                    allowSwapping: false,
                    allowCombine: false,
                    user: null,
                    createNetworkEvent: createNetworkEvent,
                    ignoreCondition: false,
                    triggerOnInsertedEffects: true) == true;
            }
            finally
            {
                EndQuickSlotMutation?.Invoke(weaponItem);
            }
        }

        private static ItemPrefab? FindItemPrefab(string itemIdentifier)
        {
            Identifier identifier = itemIdentifier.ToIdentifier();
            if (ItemPrefab.Prefabs.TryGet(identifier, out ItemPrefab? prefab))
            {
                return prefab;
            }

            return (MapEntityPrefab.FindByIdentifier(identifier) ??
                    MapEntityPrefab.FindByName(itemIdentifier)) as ItemPrefab;
        }

        private static void RemoveMismatchedSlotItems(Item weaponItem, int slotIndex, Identifier identifier)
        {
            if (weaponItem.OwnInventory == null ||
                slotIndex < 0 ||
                slotIndex >= weaponItem.OwnInventory.slots.Length)
            {
                return;
            }

            Item? item = weaponItem.OwnInventory.slots[slotIndex].FirstOrDefault();
            if (item != null && !item.Removed && item.Prefab.Identifier != identifier)
            {
                weaponItem.OwnInventory.RemoveItem(item);
                Entity.Spawner.AddItemToRemoveQueue(item);
            }
        }

        private static void RemoveSpawnedItem(Item? item)
        {
            if (item != null && !item.Removed)
            {
                Entity.Spawner.AddItemToRemoveQueue(item);
            }
        }
    }
}
