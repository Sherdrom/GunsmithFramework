GunsmithFramework = {
    EmptyPartId = "__empty",
    Config = {
        parts = {
            alternative = { nameKey = "part.alternative" },
            empty = { nameKey = "part.empty" },
            installed = { nameKey = "part.installed" }
        }
    },
    Core = {
        EmptyPartForPath = function() return "empty" end,
        EncodePreview = function() return "" end,
        EncodeText = tostring,
        FrameworkLocalizationKey = function(key) return key end,
        GetPartsForType = function() return { "empty", "alternative" } end,
        HasChildSlots = function() return false end,
        IsPartCompatible = function() return true end,
        IsRequiredSlot = function() return true end,
        ItemIdentifier = function() return "weapon" end,
        NormalizeUiPath = function(_, path) return path end,
        OwnerForWeaponId = function() return "owner" end,
        PathLabel = function() return "path" end,
        SlotsForPath = function()
            return { { path = "rear_sight_mount", partType = "rear_sight_mount", nameKey = "rear_sight_mount" } }
        end,
        UiParentPath = function() return "" end,
        WeaponConfig = function() return {} end
    },
    Inventory = {
        ActorForItem = function() return nil end,
        HasPartItem = function() return true end
    },
    Stats = {
        Encode = function() return "" end,
        PartStats = function() return {} end,
        SumSelection = function() return {} end
    }
}

dofile("Lua/Scripts/Gunsmith/UiSpec.lua")

local spec = GunsmithFramework.UiSpec.Build(
    {},
    { rear_sight_mount = "installed" },
    {},
    "")

assert(
    spec:find("installed:part.installed:installed", 1, true),
    "UiSpec must include the current part even when the candidate cache omits it")
assert(
    spec:find("__empty:ui.empty_required_part:available", 1, true),
    "Important slots must stay removable and warn that the weapon will not fire")

local emptySpec = GunsmithFramework.UiSpec.Build(
    {},
    { rear_sight_mount = "empty" },
    {},
    "")
local _, emptyEntryCount = emptySpec:gsub("__empty:", "")
assert(emptyEntryCount == 1, "emptyPart must be represented by exactly one synthetic empty entry")
assert(not emptySpec:find("empty:part.empty:", 1, true), "emptyPart must not appear as a second candidate")
assert(
    emptySpec:find("rear_sight_mount|rear_sight_mount||0|__empty:ui.empty_required_part:installed", 1, true),
    "emptyPart must be exposed to the UI as the installed empty state")

print("UiSpec current-part fallback test passed")
