local realDofile = dofile
local loads = {}

GunsmithFramework = {
    ScriptPath = "Lua/Scripts/Gunsmith",
    PendingPackages = {
        {
            id = "before",
            modDir = "before",
            entry = "any/path/before.lua",
            localizationFiles = {},
            localizationPrefix = "before",
            weaponTags = {},
            partTags = {}
        }
    }
}

dofile = function(path)
    local weaponId = string.match(path, "([^/]+)%.lua$")
    if weaponId ~= "before" and weaponId ~= "after" then return end
    loads[weaponId] = (loads[weaponId] or 0) + 1
    GunsmithFramework.Config.weapons[weaponId] = { platform = "test" }
end

realDofile("Lua/Scripts/Gunsmith/Main.lua")

GunsmithFramework.RegisterPackage({
    id = "after",
    modDir = "after",
    entry = "another/path/after.lua",
    localizationFiles = {},
    localizationPrefix = "after",
    weaponTags = {},
    partTags = {}
})

assert(GunsmithFramework.PendingPackages == nil)
assert(loads.before == 1)
assert(loads.after == 1)
assert(GunsmithFramework.Owners.weapons.before == "before")
assert(GunsmithFramework.Owners.weapons.after == "after")

dofile = realDofile
print("Packages register before or after the framework with arbitrary entry paths")
