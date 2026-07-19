local handguardPath = "receiver/upper_receiver/handguard"
local selection = { [handguardPath] = "old_handguard" }
local returned
local consumed
local saves = 0

GunsmithFramework = {
    Config = {
        parts = {
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
        GetPart = function(partId)
            return partId == "old_handguard" and { item = { identifier = "old_handguard_item" } } or nil
        end,
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
            consumed = part.item.identifier
            return true
        end,
        ReturnPartItem = function(_, part, callback)
            assert(part.item.identifier == "old_handguard_item")
            returned = callback
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
assert(selection[handguardPath] == "new_handguard" and consumed == "new_handguard_item" and saves == 1 and returned)
returned()
assert(saves == 2)
print("Server handguard replacement consumes the new part and returns the old one")
