local item = {}
local selection = {}
local platform = {}
local slots = { { key = "lower_rail", path = "receiver.handguard.lower" } }
local parts = {
    grip = {},
    launcher = { quickAttachmentTransform = { muzzleOutletOffset = { x = 135, y = 23 } } }
}
local hooks = {}
GunsmithFramework = {
    Config = { weapons = {} },
    Core = {
        PlatformConfig = function(value) return value == item and platform or nil end,
        ItemKey = function(value) return value end,
        QuickSlotsForSelection = function() return slots end,
        GetInstalledPart = function(current, path) return parts[current[path]] end
    },
    State = { selections = { [item] = selection }, loadedStates = { [item] = true } }
}
Hook = {
    Add = function(name, _, callback) hooks[name] = callback end,
    Patch = function() end,
    HookMethodType = { After = 1 }
}
CLIENT = false
SERVER = true
dofile("Lua/Scripts/Gunsmith/Runtime.lua")
dofile("Lua/Scripts/Gunsmith/Hooks.lua")
GunsmithFramework.Hooks.Register()

local hasBarrel = assert(hooks.GunsmithFrameworkHasLowerRailBarrel)
assert(not hasBarrel({}))
assert(not hasBarrel(item))
selection[slots[1].path] = "__empty"
assert(not hasBarrel(item))
selection[slots[1].path] = "grip"
assert(not hasBarrel(item))
selection[slots[1].path] = "launcher"
assert(hasBarrel(item))
selection[slots[1].path] = nil
assert(not hasBarrel(item))
slots = {}
assert(not hasBarrel(item))
print("Lower-rail barrel diagnostics require an installed part with a muzzle outlet")
