local function makeInventory(owner, items)
    return setmetatable({ Owner = owner }, {
        __index = function(_, key)
            if key == "slots" then error("private slots accessed") end
            if key ~= "AllItemsMod" then return nil end
            local index = 0
            return function()
                index = index + 1
                return items[index]
            end
        end
    })
end

GunsmithFramework = {
    Core = { ItemIdentifier = function(item) return item.identifier end }
}
LuaUserData = { IsTargetType = function(item) return item.kind == "item" end }

local character = {}
local sourceItem = { kind = "item", identifier = "weapon", removed = false }
local installedPart = { kind = "item", identifier = "grip", removed = false }
local backpack = { kind = "item", identifier = "backpack", removed = false }
local availablePart = { kind = "item", identifier = "grip", removed = false }
local characterInventory = makeInventory(character, { sourceItem, backpack })
local sourceInventory = makeInventory(sourceItem, { installedPart })
local backpackInventory = makeInventory(backpack, { availablePart })

character.Inventory = characterInventory
sourceItem.ParentInventory = characterInventory
sourceItem.OwnInventory = sourceInventory
installedPart.ParentInventory = sourceInventory
backpack.ParentInventory = characterInventory
backpack.OwnInventory = backpackInventory
availablePart.ParentInventory = backpackInventory

dofile("Lua/Scripts/Gunsmith/Inventory.lua")

local Inventory = GunsmithFramework.Inventory
assert(Inventory.FindPartItem(character, "grip", sourceItem) == availablePart)
local ids = Inventory.CollectAvailableItemIds(character, sourceItem)
assert(ids.grip and ids.backpack and not ids.weapon)
print("Inventory uses public Barotrauma inventory enumeration")
