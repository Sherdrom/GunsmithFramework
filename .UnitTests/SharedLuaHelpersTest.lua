GunsmithFramework = { Config = { parts = {} } }

dofile("Lua/Scripts/Gunsmith/Core.lua")
dofile("Lua/Scripts/Gunsmith/Stats.lua")
dofile("Lua/Scripts/Gunsmith/UiSpec.lua")

local Core = GunsmithFramework.Core
local Stats = GunsmithFramework.Stats
local UiSpec = GunsmithFramework.UiSpec

local requiredPlatform = {
    rootSlots = { { path = "receiver", hidden = true } },
    requiredSlots = { "upper_receiver/barrel", "stock_mount" }
}
assert(Core.HasMissingRequiredParts({}, requiredPlatform))
assert(Core.HasMissingRequiredParts({ receiver = "receiver" }, requiredPlatform))
assert(not Core.HasMissingRequiredParts({
    receiver = "receiver",
    ["receiver/upper_receiver/barrel"] = "barrel",
    ["receiver/stock_mount"] = "stock"
}, requiredPlatform))
assert(not Core.HasMissingRequiredParts({ receiver = "receiver" }, { rootSlots = requiredPlatform.rootSlots, requiredSlots = "upper_receiver/barrel" }))

assert(not Core.PartProvidesAccepted(nil, { "mount" }))
assert(not Core.PartProvidesAccepted(42, { "mount" }))
assert(not Core.PartProvidesAccepted({}, { "mount" }))
assert(not Core.PartProvidesAccepted({ provides = { "mount" } }, "mount"))
assert(Core.PartProvidesAccepted({ provides = { "mount" } }, { "mount" }))
assert(not Core.PartProvidesAccepted({ provides = { "mount" } }, { "other" }))
assert(Core.PartProvidesAccepted({ provides = { "a", "mount" } }, { "other", "mount" }))

GunsmithFramework.Config.parts = {
    alpha = { type = "sight" },
    zeta = { type = "sight" },
    first = { type = "sight", uiOrder = -100 },
    last = { type = "sight", uiOrder = 10 },
    ignored = { type = "other", uiOrder = -1000 }
}
GunsmithFramework.Owners = {
    parts = {
        alpha = "test",
        zeta = "test",
        first = "test",
        last = "test",
        ignored = "test"
    }
}
GunsmithFramework.Packages = { test = { _importSet = {}, localizationPrefix = "test.gunsmith" } }
assert(table.concat(Core.GetPartsForType("sight", "test"), ",") == "first,alpha,zeta,last")

assert(UiSpec.EncodePartEntry("part-id", {
    nameKey = "part.name",
    stats = { Ergonomics = 1.25 },
    item = { identifier = "item:one" },
    visual = {
        texture = "texture|path",
        source = { x = 1, y = 2, w = 3, h = 4 }
    }
}, "installed") == "part-id:part.name:installed:Ergonomics=1.2500:item%3Aone:texture%7Cpath:1%2C2%2C3%2C4")

GunsmithFramework.Config.parts.alpha.item = { identifier = "alpha_item" }
GunsmithFramework.Config.parts.alpha.stats = { Ergonomics = 1.25, AttackMultiplier = -0.1 }
assert(Stats.TooltipSpec({ Prefab = { Identifier = { Value = "alpha_item" } } })
    == "test.gunsmith::Ergonomics=1.2500,AttackMultiplier=-0.1000")

GunsmithFramework.Config.parts.zeta.stats = { Ergonomics = -0.25, AttackMultiplier = 0.2 }
GunsmithFramework.Config.weapons = { test_weapon = { platform = "test_platform" } }
GunsmithFramework.Config.platforms = { test_platform = {} }
GunsmithFramework.Owners.weapons = { test_weapon = "test" }
GunsmithFramework.Owners.platforms = { test_platform = "test" }
GunsmithFramework.Runtime = {
    GetSelection = function() return { a = "alpha", z = "zeta" } end
}
assert(Stats.TooltipSpec({ Prefab = { Identifier = { Value = "test_weapon" } } })
    == "test.gunsmith::Ergonomics=1.0000,AttackMultiplier=0.1000")

print("Shared Lua helpers preserve matching and UI encoding")
