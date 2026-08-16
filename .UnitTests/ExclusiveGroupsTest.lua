local parts = {
    receiver = { type = "receiver", provides = { "receiver" } },
    short_barrel = {
        type = "barrel",
        provides = { "barrel" },
        groups = { "test:short_barrel" }
    },
    other_short_barrel = {
        type = "barrel",
        provides = { "barrel" },
        groups = { "test:short_barrel" }
    },
    long_handguard = {
        type = "handguard",
        provides = { "handguard" },
        groups = { "test:long_handguard" }
    },
    compatible_handguard = {
        type = "handguard",
        provides = { "handguard" },
        groups = { "test:short_handguard" }
    },
    excluded_barrel = {
        type = "barrel",
        provides = { "barrel" },
        excludes = { "compatible_handguard" }
    }
}

local weapon = {
    platform = "test_platform",
    roots = {
        barrel = { part = "short_barrel", socket = { x = 0, y = 0 } },
        handguard = { part = "long_handguard", socket = { x = 0, y = 0 } }
    }
}

GunsmithFramework = {
    Config = {
        platforms = {
            test_platform = {
                rootSlots = {
                    { path = "barrel" },
                    { path = "handguard" }
                }
            }
        },
        weapons = { test_weapon = weapon },
        parts = parts,
        exclusiveGroups = {
            ["test:short_barrel"] = { "test:long_handguard" }
        }
    },
    Owners = {
        platforms = { test_platform = "test" },
        weapons = { test_weapon = "test" },
        parts = {
            receiver = "test",
            short_barrel = "test",
            other_short_barrel = "test",
            long_handguard = "test",
            compatible_handguard = "test",
            excluded_barrel = "test"
        }
    },
    Packages = { test = { _importSet = {} } }
}

dofile("Lua/Scripts/Gunsmith/Core.lua")

local Core = GunsmithFramework.Core
local selection = { barrel = "short_barrel" }
assert(Core.PartConflictsWithSelection(selection, "handguard", "long_handguard"))
assert(not Core.PartConflictsWithSelection(selection, "handguard", "compatible_handguard"))
assert(not Core.PartConflictsWithSelection(selection, "other_barrel", "other_short_barrel"))
assert(Core.PartConflictsWithSelection(
    { handguard = "long_handguard" },
    "barrel",
    "short_barrel"))

assert(Core.PartConflictsWithSelection(
    { handguard = "compatible_handguard" },
    "barrel",
    "excluded_barrel"))
assert(Core.PartConflictsWithSelection(
    { barrel = "excluded_barrel" },
    "handguard",
    "compatible_handguard"))

local defaults = Core.BuildDefaultSelection(
    GunsmithFramework.Config.platforms.test_platform,
    weapon,
    "test")
assert(defaults.barrel == "short_barrel")
assert(defaults.handguard == nil)

dofile("Lua/Scripts/Gunsmith/Validation.lua")

local validationOutput = {}
local originalPrint = print
print = function(message)
    table.insert(validationOutput, tostring(message))
end
local validationOk, validationResult = pcall(GunsmithFramework.Validation.Run, {
    platforms = {},
    weapons = {},
    parts = {
        bad_groups = {
            type = "receiver",
            nameKey = "test.part.bad_groups",
            provides = { "receiver" },
            groups = { "test:duplicate", "test:duplicate" },
            item = { virtual = true }
        }
    },
    exclusiveGroups = {
        ["test:self"] = { "test:self" },
        ["test:duplicate"] = { "test:other", "test:other" }
    }
}, "ExclusiveGroupsTest")
print = originalPrint

assert(validationOk)
assert(not validationResult)
local validationReport = table.concat(validationOutput, "\n")
assert(validationReport:find("groups contains duplicate group 'test:duplicate'", 1, true))
assert(validationReport:find("Exclusive group 'test:self' excludes itself", 1, true))
assert(validationReport:find("Exclusive group 'test:duplicate' excludes duplicate group 'test:other'", 1, true))

print("exclusiveGroups relate part groups symmetrically and prune defaults deterministically")
