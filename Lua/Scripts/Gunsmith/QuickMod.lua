GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = Gunsmith.Core
local QuickMod = {}
Gunsmith.QuickMod = QuickMod

local itemIdentifierToPartIds = nil

local function quickSlotsForItem(item)
    local weapon = Core.WeaponConfig(item)
    if not weapon then return nil end
    local platform = Core.PlatformConfig(item)
    local Runtime = Gunsmith.Runtime
    local selection = Runtime and Runtime.GetSelection and Runtime.GetSelection(item) or nil
    if selection and platform and Core.QuickSlotsForSelection then
        return Core.QuickSlotsForSelection(item, selection, platform)
    end
    if type(weapon.quickSlots) == "table" then return weapon.quickSlots end
    return nil
end

local function rebuildItemIndex()
    itemIdentifierToPartIds = {}
    for partId, part in pairs(Gunsmith.Config.parts) do
        local identifier = part and part.item and part.item.identifier or nil
        if identifier and identifier ~= "" then
            itemIdentifierToPartIds[identifier] = itemIdentifierToPartIds[identifier] or {}
            table.insert(itemIdentifierToPartIds[identifier], partId)
        end
    end
end

local function partIdsForItemIdentifier(identifier)
    if not itemIdentifierToPartIds then
        rebuildItemIndex()
    end
    return itemIdentifierToPartIds[identifier] or {}
end

local function slotItem(item, slotIndex)
    if not item or not item.OwnInventory or not item.OwnInventory.slots then return nil end
    local slot = item.OwnInventory.slots[slotIndex + 1]
    if not slot or not slot.items then return nil end
    for _, contained in pairs(slot.items) do
        if contained and not contained.removed then
            return contained
        end
    end
    return nil
end

function QuickMod.HasSlotItem(item, slotIndex)
    return slotItem(item, slotIndex) ~= nil
end

local function findCompatiblePartId(selection, platform, path, identifier, ownerId)
    if not Core.IsValidPath(selection, platform, path) then return nil end
    for _, partId in ipairs(partIdsForItemIdentifier(identifier)) do
        if Core.IsPartCompatible(selection, platform, path, partId, ownerId) then
            return partId
        end
    end
    return nil
end

function QuickMod.PartIdForItem(selection, platform, path, item, ownerId)
    return findCompatiblePartId(selection, platform, path, Core.ItemIdentifier(item), ownerId)
end

local function beginQuickSlotMutation(item)
    if Hook and Hook.Call then
        Hook.Call("GunsmithFrameworkBeginQuickSlotMutation", item)
    end
end

local function endQuickSlotMutation(item)
    if Hook and Hook.Call then
        Hook.Call("GunsmithFrameworkEndQuickSlotMutation", item)
    end
end

function QuickMod.IsQuickPath(item, path)
    local quickSlots = quickSlotsForItem(item)
    if not quickSlots or not path then return false end
    for _, quickSlot in ipairs(quickSlots) do
        if quickSlot.path == path then return true end
    end
    return false
end

function QuickMod.IsQuickItem(item)
    local quickSlots = quickSlotsForItem(item)
    return quickSlots ~= nil and #quickSlots > 0
end

function QuickMod.SlotForPath(item, path)
    local quickSlots = quickSlotsForItem(item)
    if not quickSlots or not path then return nil end
    for _, quickSlot in ipairs(quickSlots) do
        if quickSlot.path == path then return quickSlot.slot end
    end
    return nil
end

function QuickMod.CanSlotAcceptItemIdentifier(item, slotIndex, identifier)
    if not item or not item.OwnInventory or slotIndex == nil or not identifier or identifier == "" then return false end
    if QuickMod.HasSlotItem(item, slotIndex) then return true end
    if not ItemPrefab or not ItemPrefab.GetItemPrefab then return true end

    local prefab = ItemPrefab.GetItemPrefab(identifier)
    if not prefab then return false end

    return item.OwnInventory.CanBePutInSlot(prefab, slotIndex, nil) == true
end

function QuickMod.CanSlotAcceptItem(item, slotIndex, partItem)
    if not item or not item.OwnInventory or slotIndex == nil or not partItem then return false end
    if QuickMod.HasSlotItem(item, slotIndex) then return true end

    return item.OwnInventory.CanBePutInSlot(partItem, slotIndex) == true
end

function QuickMod.SyncFromContainer(item, selection, platform)
    local quickSlots = quickSlotsForItem(item)
    if not quickSlots or not selection or not platform then return false end
    local ownerId = Core.OwnerForWeaponId(Core.ItemIdentifier(item))

    local changed = false
    for _, quickSlot in ipairs(quickSlots) do
        local path = quickSlot.path
        local contained = slotItem(item, quickSlot.slot)
        local newPartId = nil
        if contained then
            newPartId = findCompatiblePartId(selection, platform, path, Core.ItemIdentifier(contained), ownerId)
        end

        if newPartId then
            if selection[path] ~= newPartId then
                selection[path] = newPartId
                changed = true
            end
        elseif selection[path] ~= nil and not Core.IsRequiredSlot(platform, path) then
            selection[path] = nil
            changed = true
        end
    end

    if changed then
        Core.InvalidateQuickSlotsCache(item)
        if Gunsmith.QuickUiSpec then Gunsmith.QuickUiSpec.InvalidateCache(item) end
    end
    return changed
end

function QuickMod.InstallPartItem(item, character, part, slotIndex)
    if SERVER then return false end
    if not item or not item.OwnInventory or not part or not part.item or not part.item.identifier then return false end
    local Inventory = Gunsmith.Inventory
    if not Inventory then return false end

    local partItem = Inventory.FindPartItem(character, part.item.identifier, item)
    if not partItem then return false end

    beginQuickSlotMutation(item)
    local result = item.OwnInventory.TryPutItem(partItem, slotIndex, true, false, character, true, false)
    endQuickSlotMutation(item)
    return result == true
end

function QuickMod.InstallSpecificPartItem(item, character, partItem, slotIndex)
    if SERVER then return false end
    if not item or not item.OwnInventory or not partItem then return false end

    beginQuickSlotMutation(item)
    local result = item.OwnInventory.TryPutItem(partItem, slotIndex, true, false, character, true, false)
    endQuickSlotMutation(item)
    return result == true
end

function QuickMod.ClearSlot(item, character, slotIndex, onReturned)
    if SERVER then return false end
    local contained = slotItem(item, slotIndex)
    if not contained then return true end

    local inventory = character and character.Inventory or nil
    beginQuickSlotMutation(item)
    local returned = inventory and inventory.TryPutItem(contained, character, CharacterInventory.AnySlot, true, true, false) == true
    if not returned then
        contained.Drop(character, true, true)
    end
    endQuickSlotMutation(item)

    local function notifyReturned()
        if onReturned then onReturned(contained) end
    end
    if Timer and Timer.Wait then
        Timer.Wait(notifyReturned, 1)
    else
        notifyReturned()
    end

    return true

end
