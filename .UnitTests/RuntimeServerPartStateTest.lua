local stockMountPath = "receiver/stock_mount"
local stockPath = stockMountPath .. "/stock"
local selection = {
    [stockMountPath] = "hket_buffer_tube",
    [stockPath] = "hket_stock"
}
local returned = {}
local consumed

GunsmithFramework = {
    Config = {
        parts = {
            hket_buffer_tube = { item = { identifier = "hket_buffer_tube_item" } }
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
            return GunsmithFramework.Config.parts[partId] or { item = { identifier = partId .. "_item" } }
        end,
        IsPartCompatible = function() return true end,
        SortedSelectionPaths = function(value)
            local paths = {}
            for path in pairs(value) do table.insert(paths, path) end
            table.sort(paths)
            return paths
        end,
        PruneInvalidSelections = function() end,
        ClearDescendants = function(value, path)
            for selectedPath in pairs(value) do
                if selectedPath:sub(1, #path + 1) == path .. "/" then
                    value[selectedPath] = nil
                end
            end
        end,
        ApplyMountDefaultsForPath = function()
            error("interactive installation must not add default child parts")
        end,
        InvalidateQuickSlotsCache = function() end
    },
    Persistence = {
        Save = function() end,
        Encode = function(value)
            if value[stockMountPath] == nil then
                assert(value[stockPath] == nil)
                return [[{"v":1,"parts":{"receiver/stock_mount":"__empty"}}]]
            end
            assert(value[stockMountPath] == "hket_buffer_tube" and value[stockPath] == nil)
            return [[{"v":1,"parts":{"receiver/stock_mount":"hket_buffer_tube","receiver/stock_mount/stock":"__empty"}}]]
        end
    },
    Inventory = {
        ItemIdentifierForPart = function(part) return part.item.identifier end,
        ConsumePartItem = function(_, part)
            consumed = part.item.identifier
            return true
        end,
        ReturnPartItem = function(_, part)
            returned[part.item.identifier] = true
            return true
        end
    },
    Stats = {},
    State = { selections = { weapon = selection } }
}
SERVER = true

dofile("Lua/Scripts/Gunsmith/Runtime.lua")
GunsmithFramework.Runtime.Apply = function() end

local state = GunsmithFramework.Runtime.SetPartFromClient(
    { removed = false }, {}, stockMountPath, GunsmithFramework.EmptyPartId)

assert(state == [[{"v":1,"parts":{"receiver/stock_mount":"__empty"}}]])
assert(returned.hket_buffer_tube_item and returned.hket_stock_item)

local installedState = GunsmithFramework.Runtime.SetPartFromClient(
    { removed = false }, {}, stockMountPath, "hket_buffer_tube")
assert(installedState == [[{"v":1,"parts":{"receiver/stock_mount":"hket_buffer_tube","receiver/stock_mount/stock":"__empty"}}]])
assert(consumed == "hket_buffer_tube_item" and selection[stockPath] == nil)
print("Server stock-mount removal returns the subtree and reinstalling the tube leaves its stock slot empty")
