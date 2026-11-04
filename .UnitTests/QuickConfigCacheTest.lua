GunsmithFramework = { Config = { parts = { first = { item = { identifier = "first_item" } } } } }
dofile("Lua/Scripts/Gunsmith/Core.lua")
local g = GunsmithFramework
local c = g.Core
c.IsValidPath = function() return true end
c.IsPartCompatible = function() return true end
c.ItemIdentifier = function(x) return x end
dofile("Lua/Scripts/Gunsmith/QuickMod.lua")
assert(g.QuickMod.PartIdForItem({}, {}, "rail", "first_item", "owner") == "first")
g.Config.parts.second = { item = { identifier = "second_item" } }
local invalidated = 0
g.QuickUiSpec = { InvalidateCache = function(item)
    assert(item == nil)
    invalidated = invalidated + 1
end }
c.ClearConfigCaches()
assert(g.QuickMod.PartIdForItem({}, {}, "rail", "second_item", "owner") == "second")
assert(invalidated == 1)

local version = "before"
g.Stats = { SumSelection = function() return {} end, Encode = function() return version end }
c.WeaponConfig = function() return {} end
c.OwnerForWeapon = function() return "owner" end
c.QuickSlotsForSelection = function() return {} end
c.EncodePreview = function() return "" end
c.FrameworkLocalizationKey = function(key) return key end
dofile("Lua/Scripts/Gunsmith/QuickUiSpec.lua")
local item, selection = {}, {}
local before = g.QuickUiSpec.Build(item, selection, {})
version = "after"
assert(g.QuickUiSpec.Build(item, selection, {}) == before)
c.ClearConfigCaches()
assert(g.QuickUiSpec.Build(item, selection, {}) ~= before, "registration must invalidate existing UI entries")
print("Registration cache reset refreshes QuickMod identifiers and all QuickUI entries")
