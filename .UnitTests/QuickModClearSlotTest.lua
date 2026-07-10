local movedItem = { removed = false }
local weaponInventory = { slots = { { items = { movedItem } } } }
local character = { Inventory = {} }
local calls = {}

function character.Inventory.TryPutItem(item, actor, allowedSlots, createNetworkEvent, ignoreCondition, triggerEffects)
    assert(item == movedItem)
    assert(actor == character)
    assert(allowedSlots == CharacterInventory.AnySlot)
    assert(createNetworkEvent and ignoreCondition and not triggerEffects)
    weaponInventory.slots[1].items = {}
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
print("QuickMod.ClearSlot preserves the original item")
