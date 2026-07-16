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
                    if item and not item.removed and item ~= sourceItem and not isInSourceItemInventory(item, sourceItem) then
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

    local item = Inventory.FindPartItem(character, identifier, sourceItem)
    if not item then return false end

    if Entity and Entity.Spawner and Entity.Spawner.AddItemToRemoveQueue then
        Entity.Spawner.AddItemToRemoveQueue(item)
    else
        local parent = parentInventory(item)
        if parent and parent.RemoveItem then
            parent.RemoveItem(item)
        end
    end
    return true
end

function Inventory.ReturnPartItem(character, part, onReturned, sourceItem)
    local identifier = Inventory.ItemIdentifierForPart(part)
    if not identifier or not ItemPrefab or not Entity or not Entity.Spawner then return false end

    local prefab = ItemPrefab.GetItemPrefab(identifier)
    if not prefab then
        print("[GunsmithFramework] Cannot return missing part item prefab: " .. tostring(identifier))
        return false
    end

    local function notifyReturned(spawned)
        if onReturned then
            onReturned(spawned)
        end
    end

    local inventory = characterInventory(character)
    if inventory then
        Entity.Spawner.AddItemToSpawnQueue(prefab, inventory, nil, nil, notifyReturned)
        return true
    end

    if character and character.WorldPosition then
        Entity.Spawner.AddItemToSpawnQueue(prefab, character.WorldPosition, nil, nil, notifyReturned)
        return true
    end

    if sourceItem and sourceItem.WorldPosition then
        Entity.Spawner.AddItemToSpawnQueue(prefab, sourceItem.WorldPosition, nil, nil, notifyReturned)
        return true
    end

    return false
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
                    if slotItem and not slotItem.removed and slotItem ~= sourceItem and not isInSourceItemInventory(slotItem, sourceItem) then
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
