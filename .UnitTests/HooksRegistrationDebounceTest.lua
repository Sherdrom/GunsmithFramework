local callbacks = {}
local items = {
    { weapon = true },
    { weapon = false }
}
local itemIndex = 0
local weaponChecks = 0
local applied = 0

GunsmithFramework = {
    Config = { weapons = { test_weapon = {} } },
    Core = {
        WeaponConfig = function(item)
            weaponChecks = weaponChecks + 1
            return item and item.weapon and {} or nil
        end
    },
    Persistence = {},
    Runtime = {
        EnsureApplied = function() applied = applied + 1 end
    },
    NpcPresets = {
        TryApply = function() return false end
    }
}
Timer = {
    Wait = function(callback, delay)
        assert(delay == 100)
        table.insert(callbacks, callback)
    end
}
Item = {
    ItemList = function()
        itemIndex = itemIndex + 1
        local item = items[itemIndex]
        if not item then itemIndex = 0 end
        return item
    end
}

dofile("Lua/Scripts/Gunsmith/Hooks.lua")
GunsmithFramework.Hooks.RefreshRegistrations()
GunsmithFramework.Hooks.RefreshRegistrations()

assert(#callbacks == 2)
callbacks[1]()
assert(applied == 0 and weaponChecks == 0)
callbacks[2]()
assert(applied == 1 and weaponChecks == 2)

print("Registration refresh is debounced and rejects non-weapons early")
