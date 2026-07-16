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
print("QuickMod.ClearSlot preserves the original item on client and server")
