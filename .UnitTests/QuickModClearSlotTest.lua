local movedItem = { removed = false }
local weaponInventory = { items = { movedItem } }
local character = { Inventory = {} }
local calls = {}

function weaponInventory.GetItemsAt(slotIndex)
    assert(slotIndex == 0)
    local index = 0
    return function()
        index = index + 1
        return weaponInventory.items[index]
    end
end

function character.Inventory.TryPutItem(item, actor, allowedSlots, createNetworkEvent, ignoreCondition, triggerEffects)
    assert(item == movedItem)
    assert(actor == character)
    assert(allowedSlots == CharacterInventory.AnySlot)
    assert(createNetworkEvent and ignoreCondition and not triggerEffects)
    weaponInventory.items = {}
    table.insert(calls, "move")
    return true
end

GunsmithFramework = {
    Core = {},
    Config = { parts = {} }
}
SERVER = false
CharacterInventory = { AnySlot = {} }
Hook = { Call = function(name) table.insert(calls, name) end }
Timer = { Wait = function(callback) calls.callback = callback end }

dofile("Lua/Scripts/Gunsmith/QuickMod.lua")

local returnedItem
assert(GunsmithFramework.QuickMod.ClearSlot({ OwnInventory = weaponInventory }, character, 0, function(item)
    returnedItem = item
end))
assert(table.concat(calls, ",") == "GunsmithFrameworkBeginQuickSlotMutation,move,GunsmithFrameworkEndQuickSlotMutation")
assert(returnedItem == nil)
calls.callback()
assert(returnedItem == movedItem)

SERVER = true
weaponInventory.items = { movedItem }
calls = {}
returnedItem = nil
assert(GunsmithFramework.QuickMod.ClearSlot({ OwnInventory = weaponInventory }, character, 0, function(item)
    returnedItem = item
end))
assert(table.concat(calls, ",") == "GunsmithFrameworkBeginQuickSlotMutation,move,GunsmithFrameworkEndQuickSlotMutation")
assert(returnedItem == nil)
calls.callback()
assert(returnedItem == movedItem)

local stockPath = "receiver/stock"
local selection = { [stockPath] = "default_stock" }
local weapon = { OwnInventory = weaponInventory }
weaponInventory.items = {}
GunsmithFramework.Core.WeaponConfig = function() return { quickSlots = { { path = stockPath, slot = 0 } } } end
GunsmithFramework.Core.PlatformConfig = function() return {} end
GunsmithFramework.Core.ItemIdentifier = function() return "weapon" end
GunsmithFramework.Core.OwnerForWeaponId = function() return "owner" end
GunsmithFramework.Core.IsRequiredSlot = function() return false end
GunsmithFramework.Core.InvalidateQuickSlotsCache = function() end
GunsmithFramework.Runtime = { GetSelection = function() return selection end }

assert(not GunsmithFramework.QuickMod.SyncFromContainer(weapon, selection, {}))
assert(selection[stockPath] == "default_stock")

assert(GunsmithFramework.QuickMod.SyncFromContainer(weapon, selection, {}, true))
assert(selection[stockPath] == nil)

selection[stockPath] = "default_stock"
GunsmithFramework.Config.parts.default_stock = { item = { identifier = "stock_item" } }
local ensuredSlot
Hook.Call = function(name, item, slotIndex, identifier)
    assert(name == "GunsmithFrameworkEnsureQuickPartItem")
    assert(item == weapon)
    assert(identifier == "stock_item")
    ensuredSlot = slotIndex
    return true
end
assert(GunsmithFramework.QuickMod.EnsureSelectionItems(weapon, selection))
assert(ensuredSlot == 0)

weaponInventory.items = { movedItem }
ensuredSlot = nil
assert(GunsmithFramework.QuickMod.EnsureSelectionItems(weapon, selection))
assert(ensuredSlot == nil)
print("QuickMod materializes default quick parts and preserves explicit removal")
