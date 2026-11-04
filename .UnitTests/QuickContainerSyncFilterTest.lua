local weapon, ammo, attachment, invalid = {}, {}, {}, {}
local positions = { [ammo] = 0, [attachment] = 1, [invalid] = 1 }
weapon.OwnInventory = { Owner = weapon, FindIndex = function(item) return positions[item] or -1 end }
local container = { Item = weapon }
local patches, timers, syncs = {}, {}, 0
GunsmithFramework = {
    Config = { weapons = {}, parts = {} },
    Core = {
        WeaponConfig = function(item) return item == weapon and { quickSlots = { { slot = 1, path = "rail" } } } end,
        PlatformConfig = function() return {} end
    },
    Runtime = {
        GetSelection = function() return {} end,
        SyncQuickModContainerItem = function(item, correctUnchanged)
            assert(item == weapon and correctUnchanged == true)
            syncs = syncs + 1
        end
    }
}
CLIENT, SERVER = false, true
LuaUserData = { IsTargetType = function() return true end }
Hook = {
    Add = function() end, Call = function() return false end,
    HookMethodType = { Before = 0, After = 1 },
    Patch = function(_, method, _, callback, phase)
        if method == "RemoveItem" then assert(phase == Hook.HookMethodType.Before) end
        patches[method] = callback
    end
}
Timer = { Wait = function(callback) timers[#timers + 1] = callback end }
dofile("Lua/Scripts/Gunsmith/QuickMod.lua")
dofile("Lua/Scripts/Gunsmith/Hooks.lua")
GunsmithFramework.Hooks.Register()
local function remove(item)
    patches.RemoveItem(weapon.OwnInventory, { item = item })
    positions[item] = nil
    patches.OnItemRemoved(container, { containedItem = item })
end
patches.OnItemContained(container, { containedItem = ammo })
remove(ammo)
assert(#timers == 0 and syncs == 0, "ammo must not queue a part-state broadcast")
patches.OnItemContained(container, { containedItem = attachment })
remove(attachment)
assert(#timers == 1, "quick-slot events must coalesce")
timers[1]()
assert(syncs == 1)
patches.OnItemContained(container, { containedItem = invalid })
assert(#timers == 2, "even an unrecognized quick-slot item requires correction")
timers[2]()
remove(invalid)
assert(#timers == 3)
timers[3]()
assert(syncs == 3)
print("Ammo changes skip synchronization; quick-slot changes retain debounced correction")
