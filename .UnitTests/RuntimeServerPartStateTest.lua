local selection = { stock = "old" }

GunsmithFramework = {
    Config = { parts = {} },
    Core = {
        PlatformConfig = function() return {} end,
        WeaponConfig = function() return {} end,
        OwnerForWeapon = function() return "owner" end,
        ItemKey = function() return "weapon" end,
        IsValidPath = function() return true end,
        IsRequiredSlot = function() return false end,
        GetPart = function() return {} end,
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
        Save = function() end,
        Encode = function(value)
            assert(value.stock == nil)
            return [[{"v":1,"parts":{"stock":"__empty"}}]]
        end
    },
    Inventory = {
        ItemIdentifierForPart = function() return nil end
    },
    Stats = {},
    State = { selections = { weapon = selection } }
}
SERVER = true

dofile("Lua/Scripts/Gunsmith/Runtime.lua")
GunsmithFramework.Runtime.Apply = function() end

local state = GunsmithFramework.Runtime.SetPartFromClient(
    { removed = false }, {}, "stock", GunsmithFramework.EmptyPartId)

assert(state == [[{"v":1,"parts":{"stock":"__empty"}}]])
print("Server part changes return their authoritative saved state")
