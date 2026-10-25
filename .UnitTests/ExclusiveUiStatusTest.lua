local inventoryScans, encodedParts = 0, 0
GunsmithFramework = {
    EmptyPartId = "__empty",
    Config = {
        parts = {
            installed = { nameKey = "part.installed", item = { identifier = "installed_item" } },
            exclusive = { nameKey = "part.exclusive", item = { identifier = "exclusive_item" } },
            incompatible = { nameKey = "part.incompatible", item = { identifier = "incompatible_item" } },
            available = { nameKey = "part.available", item = { identifier = "available_item" } },
            missing = { nameKey = "part.missing", item = { identifier = "missing_item" } },
            virtual = { nameKey = "part.virtual" }
        }
    },
    Core = {
        EmptyPartForPath = function() return nil end,
        EncodePreview = function() return "" end,
        EncodeText = tostring,
        FrameworkLocalizationKey = function(key) return key end,
        GetPartsForType = function() return { "installed", "exclusive", "incompatible", "available", "missing", "virtual" } end,
        HasChildSlots = function() return false end,
        IsPartCompatible = function(_, _, _, partId) return partId ~= "incompatible" and partId ~= "exclusive" end,
        IsRequiredSlot = function() return false end,
        IsValidPath = function() return true end,
        ItemIdentifier = function() return "weapon" end,
        NormalizeUiPath = function(_, path) return path end,
        OwnerForWeapon = function() return "owner" end,
        OwnerForWeaponId = function() return "owner" end,
        PartConflictsWithSelection = function(_, _, partId) return partId == "exclusive" end,
        PartTypeForPath = function() return "barrel" end,
        PathLabel = function() return "path" end,
        PathNameKey = function() return "path.barrel" end,
        QuickSlotsForSelection = function()
            return { { path = "barrel", slot = 0, nameKey = "path.barrel", anchor = { x = 0, y = 0 } } }
        end,
        SlotsForPath = function()
            return { { path = "barrel", partType = "barrel", nameKey = "path.barrel" } }
        end,
        UiParentPath = function() return "" end,
        WeaponConfig = function() return {} end
    },
    Inventory = {
        ActorForItem = function() return nil end,
        CollectAvailableItemIds = function()
            inventoryScans = inventoryScans + 1
            return {
                installed_item = true,
                exclusive_item = true,
                incompatible_item = true,
                available_item = true
            }
        end,
        HasPartItem = function() error("UI must not scan inventory per candidate") end
    },
    QuickMod = {
        CanSlotAcceptItemIdentifier = function() return true end,
        HasSlotItem = function() return false end,
        SlotForPath = function() return nil end
    },
    Stats = {
        Encode = function() return "" end,
        PartStats = function() encodedParts = encodedParts + 1; return {} end,
        SumSelection = function() return {} end
    }
}

dofile("Lua/Scripts/Gunsmith/UiSpec.lua")
dofile("Lua/Scripts/Gunsmith/QuickUiSpec.lua")

local item = {}
local selection = { barrel = "installed" }
local platform = {}
local fullSpec = GunsmithFramework.UiSpec.Build(item, selection, platform, "")
assert(inventoryScans == 1, "full UI must collect inventory once")
encodedParts = 0
local quickSpec = GunsmithFramework.QuickUiSpec.Build(item, selection, platform)
assert(inventoryScans == 1, "quick UI must not collect inventory")
assert(encodedParts == 1, "quick UI only encodes the installed part")

assert(fullSpec:find("exclusive:part.exclusive:exclusive", 1, true))
assert(fullSpec:find("incompatible:part.incompatible:incompatible", 1, true))
assert(fullSpec:find("available:part.available:available", 1, true))
assert(fullSpec:find("missing:part.missing:missing", 1, true))
assert(fullSpec:find("virtual:part.virtual:available", 1, true))
assert(quickSpec:find("installed:part.installed:installed", 1, true))
assert(not quickSpec:find("part.exclusive", 1, true))
assert(not quickSpec:find("exclusive_item", 1, true))
assert(not quickSpec:find("incompatible_item", 1, true))
assert(quickSpec:find("items=installed_item~available_item~missing_item", 1, true))
assert(GunsmithFramework.QuickUiSpec.Build(item, selection, platform) == quickSpec)
assert(encodedParts == 1, "cached quick UI must not encode parts again")
selection.barrel = nil
GunsmithFramework.QuickUiSpec.InvalidateCache(item)
local emptyQuickSpec = GunsmithFramework.QuickUiSpec.Build(item, selection, platform)
assert(emptyQuickSpec:find("barrel|path.barrel||0||slot=0", 1, true))
assert(encodedParts == 1, "empty quick slot needs no part entry")

print("full UI snapshots inventory once; quick UI encodes only installed parts and compatible IDs")
