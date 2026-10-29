local parts = { receiver = { type = "receiver", mounts = {} } }
local owners = { receiver = "owner" }
for i = 1, 1000 do
    parts["part" .. i] = { type = "optic", provides = { "optic" }, item = { identifier = "item" .. i } }
    owners["part" .. i] = "owner"
end
for i = 1, 50 do
    parts.receiver.mounts[i] = { path = "slot" .. i, partType = "optic", accepts = { "optic" } }
end
GunsmithFramework = {
    Config = {
        parts = parts,
        weapons = { rifle = { platform = "base", roots = { receiver = { part = "receiver" } } } },
        platforms = { base = { rootSlots = { { path = "receiver", hidden = true } } } }
    },
    Owners = { parts = owners, weapons = { rifle = "owner" }, platforms = { base = "owner" } },
    Packages = { owner = { _importSet = {}, localizationPrefix = "owner" } }
}
dofile("Lua/Scripts/Gunsmith/Core.lua")
dofile("Lua/Scripts/Gunsmith/Runtime.lua")
local c, r = GunsmithFramework.Core, GunsmithFramework.Runtime
local checks, match = 0, c.PartProvidesAccepted
c.PartProvidesAccepted = function(...)
    checks = checks + 1
    return match(...)
end
local function rifle() return { Prefab = { Identifier = { Value = "rifle" } } } end
local ids = r.FabricatorPartItemIds(rifle())
assert(checks == 1001, "identical mount conditions must scan the part catalog only once")
assert(r.FabricatorPartItemIds(rifle()) == ids)
local categorized = r.FabricatorPartItemIds(rifle(), true)
assert(categorized:find("item1:optic:owner.path.optic", 1, true))
assert(checks == 1001, "both formats and all items sharing a weapon config reuse the catalog")
parts.late = { type = "optic", provides = { "optic" }, item = { identifier = "late_item" } }
owners.late = "owner"
c.ClearConfigCaches()
assert(r.FabricatorPartItemIds(rifle()):find("late_item", 1, true))
assert(checks == 2003, "package registration invalidates the catalog")
parts.foreign = { type = "optic", provides = { "optic" }, item = { identifier = "foreign_item" } }
owners.foreign = "foreign"
c.ClearConfigCaches()
assert(not r.FabricatorPartItemIds(rifle()):find("foreign_item", 1, true))
print("Catalog matching reduced from 50050 to 1001 checks; cached reads perform no matching")
