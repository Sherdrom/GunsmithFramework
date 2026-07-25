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

weaponInventory.items = {}
assert(GunsmithFramework.QuickMod.EnsureSelectionItems(weapon, selection))
assert(ensuredSlot == 0)

local receiverOpticPath = "receiver/optic"
local handleOpticPath = "receiver/carry_handle/optic"
local opticItem = { removed = false }
selection = { [handleOpticPath] = "optic" }
weaponInventory.items = { opticItem }
GunsmithFramework.Config.parts.optic = { item = { identifier = "optic_item" } }
GunsmithFramework.Core.WeaponConfig = function()
    return {
        quickSlots = {
            { path = receiverOpticPath, slot = 0 },
            { path = handleOpticPath, slot = 0 }
        }
    }
end
GunsmithFramework.Core.ItemIdentifier = function(item)
    return item == opticItem and "optic_item" or "weapon"
end
GunsmithFramework.Core.IsValidPath = function() return true end
GunsmithFramework.Core.IsPartCompatible = function() return true end

assert(not GunsmithFramework.QuickMod.SyncFromContainer(weapon, selection, {}, true))
assert(selection[receiverOpticPath] == nil and selection[handleOpticPath] == "optic")

assert(GunsmithFramework.QuickMod.SyncFromContainer(weapon, selection, {}, true, receiverOpticPath))
assert(selection[receiverOpticPath] == "optic" and selection[handleOpticPath] == nil)

selection[handleOpticPath] = "optic"
assert(GunsmithFramework.QuickMod.SyncFromContainer(weapon, selection, {}, true))
assert(selection[receiverOpticPath] == "optic" and selection[handleOpticPath] == nil)

weaponInventory.items = {}
assert(GunsmithFramework.QuickMod.SyncFromContainer(weapon, selection, {}, true))
assert(selection[receiverOpticPath] == nil and selection[handleOpticPath] == nil)

local hooksFile = assert(io.open(".AssemblyCSharpSource/GunsmithFramework/ClientProject/ClientSource/GunsmithHooks.cs", "r"))
local hooksSource = hooksFile:read("*a")
hooksFile:close()
local firstEditorGuard = assert(hooksSource:find("Screen.Selected?.IsEditor != true", 1, true))
assert(hooksSource:find("Screen.Selected?.IsEditor != true", firstEditorGuard + 1, true))

local luaHooksFile = assert(io.open("Lua/Scripts/Gunsmith/Hooks.lua", "r"))
local luaHooksSource = luaHooksFile:read("*a")
luaHooksFile:close()
local constructorHook = assert(luaHooksSource:find('Hook.Patch("Barotrauma.Item", ".ctor"', 1, true))
local constructorHookEnd = assert(luaHooksSource:find("Hook.HookMethodType.After)", constructorHook, true))
assert(luaHooksSource:sub(constructorHook, constructorHookEnd):find("Timer.Wait", 1, true))

local runtimeFile = assert(io.open("Lua/Scripts/Gunsmith/Runtime.lua", "r"))
local runtimeSource = runtimeFile:read("*a")
runtimeFile:close()
assert(not runtimeSource:find("initialQuickItemsEnsured", 1, true))

local spawnerFile = assert(io.open(".AssemblyCSharpSource/GunsmithFramework/SharedProject/SharedSource/GunsmithQuickPartItemSpawner.cs", "r"))
local spawnerSource = spawnerFile:read("*a")
spawnerFile:close()
assert(spawnerSource:find("Entity.Spawner == null || Entity.Spawner.Removed", 1, true))
assert(not spawnerSource:find("new(prefab, weaponItem.WorldPosition, null)", 1, true))

local quickDragFile = assert(io.open(".AssemblyCSharpSource/GunsmithFramework/ClientProject/ClientSource/GunsmithQuick/GunsmithQuickDrag.cs", "r"))
local quickDragSource = quickDragFile:read("*a")
quickDragFile:close()
assert(not quickDragSource:find("createNetworkEvent: false", 1, true))

print("QuickMod reconciles default quick parts and preserves explicit removal")
