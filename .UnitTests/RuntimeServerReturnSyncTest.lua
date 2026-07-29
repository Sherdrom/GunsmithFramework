local handguardPath = "receiver/upper_receiver/handguard"
local selection = { [handguardPath] = "old_handguard" }
local returned = {}
local consumed = {}
local saves = 0

GunsmithFramework = {
    Config = {
        parts = {
            old_handguard = { item = { identifier = "old_handguard_item" } },
            empty_handguard = { item = { virtual = true } },
            new_handguard = { item = { identifier = "new_handguard_item" } }
        }
    },
    Core = {
        PlatformConfig = function() return {} end,
        WeaponConfig = function() return {} end,
        OwnerForWeapon = function() return "owner" end,
        ItemKey = function() return "weapon" end,
        IsValidPath = function() return true end,
        IsRequiredSlot = function() return false end,
        EmptyPartForPath = function(_, path)
            return path == handguardPath and "empty_handguard" or nil
        end,
        GetPart = function(partId) return GunsmithFramework.Config.parts[partId] end,
        IsPartCompatible = function() return true end,
        SortedSelectionPaths = function(value)
            local paths = {}
            for path in pairs(value) do table.insert(paths, path) end
            table.sort(paths)
            return paths
        end,
        PruneInvalidSelections = function() end,
        ClearDescendants = function() end,
        ApplyMountDefaultsForPath = function() end,
        InvalidateQuickSlotsCache = function() end
    },
    Persistence = {
        Save = function() saves = saves + 1 end
    },
    Inventory = {
        ItemIdentifierForPart = function(part) return part.item.identifier end,
        ConsumePartItem = function(_, part)
            table.insert(consumed, part.item.identifier)
            return true
        end,
        ReturnPartItem = function(_, part, callback)
            table.insert(returned, { identifier = part.item.identifier, callback = callback })
            return true
        end
    },
    Stats = {},
    State = { selections = { weapon = selection } }
}
SERVER = true

dofile("Lua/Scripts/Gunsmith/Runtime.lua")
GunsmithFramework.Runtime.Apply = function() end

local item = { removed = false }
assert(GunsmithFramework.Runtime.SetPart(item, handguardPath, "new_handguard", nil, {}) == false)
assert(selection[handguardPath] == "new_handguard" and consumed[1] == "new_handguard_item" and saves == 1)
assert(returned[1].identifier == "old_handguard_item")
returned[1].callback()
assert(saves == 2)

assert(GunsmithFramework.Runtime.SetPart(item, handguardPath, GunsmithFramework.EmptyPartId, nil, {}) == false)
assert(selection[handguardPath] == "empty_handguard" and #consumed == 1 and saves == 3)
assert(returned[2].identifier == "new_handguard_item")
returned[2].callback()
assert(saves == 4)

local ordinaryPath = "receiver/ordinary"
selection[ordinaryPath] = "old_handguard"
assert(GunsmithFramework.Runtime.SetPart(item, ordinaryPath, GunsmithFramework.EmptyPartId, nil, {}) == false)
assert(selection[ordinaryPath] == nil and returned[3].identifier == "old_handguard_item")

print("Server part replacement maps configured emptyPart while ordinary empty slots remain nil")
