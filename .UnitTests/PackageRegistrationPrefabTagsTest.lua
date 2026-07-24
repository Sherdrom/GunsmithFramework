local realDofile = dofile
local lookupCount = 0

local function tagIterator(values)
    local index = 0
    return function()
        index = index + 1
        local value = values[index]
        return value and { Value = value } or nil
    end
end

Identifier = function(value) return { Value = value } end
MapEntityPrefab = {
    FindByIdentifier = function(identifier)
        lookupCount = lookupCount + 1
        if identifier.Value == "test_weapon" then
            return { Tags = tagIterator({ "gunsmith", "test_gunsmith" }) }
        elseif identifier.Value == "test_part_item" then
            return { Tags = tagIterator({ "smallitem", "test_gunsmith_part" }) }
        end
        return nil
    end
}

GunsmithFramework = { ScriptPath = "Lua/Scripts/Gunsmith" }
dofile = function(path)
    if path == "testmod/entry.lua" then
        GunsmithFramework.Config.weapons.test_weapon = { platform = "test_platform" }
        GunsmithFramework.Config.parts.test_part = { item = { identifier = "test_part_item" } }
    elseif path == "testmod/explicit.lua" then
        GunsmithFramework.Config.weapons.explicit_weapon_item = { platform = "test_platform" }
        GunsmithFramework.Config.parts.explicit_part_id = { item = { identifier = "explicit_part_item" } }
    end
end

realDofile("Lua/Scripts/Gunsmith/Main.lua")

GunsmithFramework.RegisterPackage({
    id = "test",
    name = "Test",
    modDir = "testmod",
    entry = "entry.lua",
    localizationFiles = {},
    localizationPrefix = "test"
})

local package = GunsmithFramework.Packages.test
assert(table.concat(package.weaponTags, ",") == "test_gunsmith")
assert(table.concat(package.partTags, ",") == "test_gunsmith_part")
assert(lookupCount == 2)

MapEntityPrefab.FindByIdentifier = function()
    error("explicit tags must skip prefab lookup")
end
GunsmithFramework.RegisterPackage({
    id = "explicit",
    name = "Explicit",
    modDir = "testmod",
    entry = "explicit.lua",
    localizationFiles = {},
    localizationPrefix = "explicit",
    weaponTags = { "explicit_weapon" },
    partTags = { "explicit_part" }
})

dofile = realDofile
print("Package registration reads loaded prefab tags and skips explicit metadata")
