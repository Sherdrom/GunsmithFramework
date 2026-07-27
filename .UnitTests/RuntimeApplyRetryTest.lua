CLIENT = true
SERVER = false

local item = { removed = false }
local selection = { handguard = "14mrs" }
local platform = { canvas = { w = 100, h = 100 } }
local applyCalls = 0
local lastSignature

GunsmithFramework = {
    Config = {
        parts = {
            ["14mrs"] = {
                visual = {
                    texture = "parts.png",
                    source = { x = 0, y = 0, w = 10, h = 10 }
                }
            }
        }
    },
    Core = {
        PlatformConfig = function() return platform end,
        WeaponConfig = function() return {} end,
        ItemKey = function(value) return value end,
        OwnerForWeapon = function() return nil end,
        SortedSelectionPaths = function() return { "handguard" } end,
        GetPart = function(partId) return GunsmithFramework.Config.parts[partId] end,
        PartVisual = function(part) return part.visual end,
        ResolveDrawOffset = function() return { x = 0, y = 0 } end,
        MountForPath = function() return nil end,
        QuickSlotsForSelection = function() return {} end,
        HasMissingRequiredParts = function() return true end,
        PruneInvalidSelections = function() end
    },
    Persistence = {},
    Stats = {
        SumSelection = function() return {} end,
        Encode = function() return "" end,
        ManagedItemIdentifiers = function() return {} end
    },
    State = {
        selections = { [item] = selection },
        loadedStates = { [item] = true }
    }
}

Hook = {
    Call = function(name, _, signature)
        if name ~= "GunsmithFrameworkApply" then return nil end
        lastSignature = signature
        applyCalls = applyCalls + 1
        return applyCalls > 1
    end
}

dofile("Lua/Scripts/Gunsmith/Runtime.lua")

GunsmithFramework.Runtime.Apply(item, true)
GunsmithFramework.Runtime.Apply(item, true)

assert(applyCalls == 2)
assert(lastSignature:find("|missingRequired:1|", 1, true))

CLIENT = false
SERVER = true
applyCalls = 0
GunsmithFramework.State.appliedConfigSignatures[item] = nil
Hook.Call = function(name, _, signature)
    if name ~= "GunsmithFrameworkApplyRuntimeState" then return nil end
    lastSignature = signature
    applyCalls = applyCalls + 1
    return applyCalls > 1
end

GunsmithFramework.Runtime.Apply(item, true)
GunsmithFramework.Runtime.Apply(item, true)

assert(applyCalls == 2)
assert(lastSignature:find("|missingRequired:1|", 1, true))
print("Failed client and server runtime applies are retried")
