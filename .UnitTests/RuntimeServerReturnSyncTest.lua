local selection = { handguard = "old" }
local returned
local saves = 0

GunsmithFramework = {
    Core = {
        PlatformConfig = function() return {} end,
        WeaponConfig = function() return {} end,
        OwnerForWeapon = function() return "owner" end,
        ItemKey = function() return "weapon" end,
        IsValidPath = function() return true end,
        IsRequiredSlot = function() return false end,
        GetPart = function(partId) return partId == "old" and { item = { identifier = "old_item" } } or nil end,
        SortedSelectionPaths = function(value)
            local paths = {}
            for path in pairs(value) do table.insert(paths, path) end
            table.sort(paths)
            return paths
        end,
        PruneInvalidSelections = function() end,
        ClearDescendants = function() end,
        InvalidateQuickSlotsCache = function() end
    },
    Persistence = {
        Save = function() saves = saves + 1 end
    },
    Inventory = {
        ItemIdentifierForPart = function(part) return part.item.identifier end,
        ReturnPartItem = function(_, _, callback)
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
assert(GunsmithFramework.Runtime.SetPart(item, "handguard", GunsmithFramework.EmptyPartId, nil, {}) == false)
assert(selection.handguard == nil and saves == 1 and returned)
returned()
assert(saves == 2)
print("Server rebroadcasts Gunsmith state after returned parts spawn")
