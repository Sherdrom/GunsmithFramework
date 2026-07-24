GunsmithFramework = {
    EmptyPartId = "__empty",
    Config = {
        parts = {
            alternative = { nameKey = "part.alternative" },
            installed = { nameKey = "part.installed" }
        }
    },
    Core = {
        EncodePreview = function() return "" end,
        EncodeText = tostring,
        FrameworkLocalizationKey = function(key) return key end,
        GetPartsForType = function() return { "alternative" } end,
        HasChildSlots = function() return false end,
        IsPartCompatible = function() return true end,
        IsRequiredSlot = function() return false end,
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

print("UiSpec current-part fallback test passed")
