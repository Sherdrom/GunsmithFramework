GunsmithFramework = {
    EmptyPartId = "__empty",
    Config = {
        parts = {
            installed = { nameKey = "part.installed", item = { identifier = "installed_item" } },
            exclusive = { nameKey = "part.exclusive", item = { identifier = "exclusive_item" } },
            incompatible = { nameKey = "part.incompatible", item = { identifier = "incompatible_item" } }
        }
    },
    Core = {
        EmptyPartForPath = function() return nil end,
        EncodePreview = function() return "" end,
        EncodeText = tostring,
        FrameworkLocalizationKey = function(key) return key end,
        GetPartsForType = function() return { "installed", "exclusive", "incompatible" } end,
        HasChildSlots = function() return false end,
        IsPartCompatible = function(_, _, _, partId) return partId ~= "incompatible" end,
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
            return {
                installed_item = true,
                exclusive_item = true,
                incompatible_item = true
            }
        end,
        HasPartItem = function() return true end
    },
    QuickMod = {
        CanSlotAcceptItemIdentifier = function() return true end,
        HasSlotItem = function() return false end,
        SlotForPath = function() return nil end
    },
    Stats = {
        Encode = function() return "" end,
        PartStats = function() return {} end,
        SumSelection = function() return {} end
    }
}

dofile("Lua/Scripts/Gunsmith/UiSpec.lua")
dofile("Lua/Scripts/Gunsmith/QuickUiSpec.lua")

local item = {}
local selection = { barrel = "installed" }
local platform = {}
local fullSpec = GunsmithFramework.UiSpec.Build(item, selection, platform, "")
local quickSpec = GunsmithFramework.QuickUiSpec.Build(item, selection, platform)

for _, spec in ipairs({ fullSpec, quickSpec }) do
    assert(spec:find("exclusive:part.exclusive:exclusive", 1, true))
    assert(spec:find("incompatible:part.incompatible:incompatible", 1, true))
end

print("exclusive conflicts use a dedicated status in full and quick part lists")
