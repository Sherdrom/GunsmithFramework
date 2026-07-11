GunsmithFramework = { Config = { parts = {} } }

dofile("Lua/Scripts/Gunsmith/Core.lua")
dofile("Lua/Scripts/Gunsmith/Stats.lua")
dofile("Lua/Scripts/Gunsmith/UiSpec.lua")

local Core = GunsmithFramework.Core
local UiSpec = GunsmithFramework.UiSpec

assert(not Core.PartProvidesAccepted(nil, { "mount" }))
assert(not Core.PartProvidesAccepted(42, { "mount" }))
assert(not Core.PartProvidesAccepted({}, { "mount" }))
assert(not Core.PartProvidesAccepted({ provides = { "mount" } }, "mount"))
assert(Core.PartProvidesAccepted({ provides = { "mount" } }, { "mount" }))
assert(not Core.PartProvidesAccepted({ provides = { "mount" } }, { "other" }))
assert(Core.PartProvidesAccepted({ provides = { "a", "mount" } }, { "other", "mount" }))

assert(UiSpec.EncodePartEntry("part-id", {
    nameKey = "part.name",
    stats = { Ergonomics = 1.25 },
    item = { identifier = "item:one" },
    visual = {
        texture = "texture|path",
        source = { x = 1, y = 2, w = 3, h = 4 }
    }
}, "installed") == "part-id:part.name:installed:Ergonomics=1.2500:item%3Aone:texture%7Cpath:1%2C2%2C3%2C4")

print("Shared Lua helpers preserve matching and UI encoding")
