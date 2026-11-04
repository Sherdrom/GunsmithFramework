GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = Gunsmith.Core
local Inventory = {}
Gunsmith.Inventory = Inventory

local parentInventory

local function currentCharacter(item)
    local inventory = parentInventory(item)
    if inventory and inventory.Owner then
        return inventory.Owner
    end
    if Character and Character.Controlled then
        return Character.Controlled
    end
    return nil
end

local function characterInventory(character)
    if character and character.Inventory then
        return character.Inventory
    end
    return nil
end

local function inventoryOwner(inventory)
    if not inventory then return nil end
    return inventory.Owner
end

parentInventory = function(item)
    if not item then return nil end
    if not LuaUserData.IsTargetType(item, "Barotrauma.Item") then return nil end
    return item.ParentInventory
end

local function isSourceInventory(inventory, sourceItem)
    if not inventory or not sourceItem then return false end
    if sourceItem.OwnInventory and inventory == sourceItem.OwnInventory then return true end
    return inventoryOwner(inventory) == sourceItem
end

local function isInSourceItemInventory(item, sourceItem)
    if not item or not sourceItem then return false end
    if item == sourceItem then return true end

    local inventory = parentInventory(item)
    local visited = {}
    while inventory do
        if visited[inventory] then return false end
        visited[inventory] = true

        if isSourceInventory(inventory, sourceItem) then return true end

        local owner = inventoryOwner(inventory)
        if not owner or owner == item then return false end
        inventory = parentInventory(owner)
    end

    return false
end

local function findItemInInventory(inventory, identifier, sourceItem, visited)
    if not inventory then return nil end
    visited = visited or {}

    local stack = { inventory }
    while #stack > 0 do
        local inv = table.remove(stack)
        if inv and not visited[inv] then
            visited[inv] = true

            if not isSourceInventory(inv, sourceItem) then
                for item in inv.AllItemsMod do
                    if item and not item.removed and not item.IsInRemoveQueue and item ~= sourceItem and not isInSourceItemInventory(item, sourceItem) then
                        if Core.ItemIdentifier(item) == identifier then
                            return item
                        end

                        if item.OwnInventory then
                            table.insert(stack, item.OwnInventory)
                        end
                    end
                end
            end
        end
    end

    return nil
end

function Inventory.ActorForItem(item)
    return currentCharacter(item)
end

function Inventory.ItemIdentifierForPart(part)
    if not part then return nil end
    if not part.item then return nil end
    return part.item.identifier
end

function Inventory.FindPartItem(character, identifier, sourceItem)
    local inventory = characterInventory(character)
    if not inventory or not identifier or identifier == "" then return nil end

    return findItemInInventory(inventory, identifier, sourceItem, {})
end

function Inventory.HasPartItem(character, part, sourceItem)
    local identifier = Inventory.ItemIdentifierForPart(part)
    if not identifier then return true end
    return Inventory.FindPartItem(character, identifier, sourceItem) ~= nil
end

function Inventory.ConsumePartItem(character, part, sourceItem)
    local identifier = Inventory.ItemIdentifierForPart(part)
    if not identifier then return true end
    if CLIENT and Game and Game.IsMultiplayer then return false end

    local item = Inventory.FindPartItem(character, identifier, sourceItem)
    if not item then return false end

    if not Entity or not Entity.Spawner or not Entity.Spawner.AddItemToRemoveQueue then return false end
    Entity.Spawner.AddItemToRemoveQueue(item)
    return true
end

function Inventory.PreparePartReturn(character, part, sourceItem)
    if CLIENT and Game and Game.IsMultiplayer then return nil end
    local identifier = Inventory.ItemIdentifierForPart(part)
    if not identifier or not ItemPrefab or not ItemPrefab.GetItemPrefab or not Entity or not Entity.Spawner
        or not Entity.Spawner.AddItemToSpawnQueue then return nil end

    local prefab = ItemPrefab.GetItemPrefab(identifier)
    if not prefab then
        print("[GunsmithFramework] Cannot return missing part item prefab: " .. tostring(identifier))
        return nil
    end

    local destination = characterInventory(character)
        or (character and character.WorldPosition) or (sourceItem and sourceItem.WorldPosition)
    if not destination then return nil end
    local spawner = Entity.Spawner
    return function(onReturned)
        spawner.AddItemToSpawnQueue(prefab, destination, nil, nil, function(spawned)
            if onReturned then onReturned(spawned) end
        end)
    end
end

function Inventory.ReturnPartItem(character, part, onReturned, sourceItem)
    local enqueue = Inventory.PreparePartReturn(character, part, sourceItem)
    if not enqueue then return false end
    enqueue(onReturned)
    return true
end

function Inventory.CollectAvailableItemIds(character, sourceItem)
    local ids = {}
    if not character or not character.Inventory then return ids end
    local visited = {}
    local stack = { character.Inventory }
    while #stack > 0 do
        local inv = table.remove(stack)
        if inv and not visited[inv] then
            visited[inv] = true

            if not isSourceInventory(inv, sourceItem) then
                for slotItem in inv.AllItemsMod do
                    if slotItem and not slotItem.removed and not slotItem.IsInRemoveQueue and slotItem ~= sourceItem and not isInSourceItemInventory(slotItem, sourceItem) then
                        local id = Core.ItemIdentifier(slotItem)
                        if id then ids[id] = true end
                        if slotItem.OwnInventory then
                            table.insert(stack, slotItem.OwnInventory)
                        end
                    end
                end
            end
        end
    end
    return ids
end
