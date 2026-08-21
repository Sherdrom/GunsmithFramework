local function item(identifier)
    return { removed = false, Prefab = { Identifier = { Value = identifier } } }
end

GunsmithFramework = {
    EmptyPartId = "__empty",
    Config = {
        platforms = { test_platform = {} },
        weapons = {
            default_weapon = { platform = "test_platform" },
            workbench_weapon = { platform = "test_platform", requiresWorkbench = true },
            ordinary_weapon = { platform = "test_platform" }
        },
        parts = {
            old = { nameKey = "part.old", item = { virtual = true } },
            new = { nameKey = "part.new", item = { virtual = true } }
        }
    },
    Owners = {
        platforms = { test_platform = "test" },
        weapons = {
            default_weapon = "test",
            workbench_weapon = "test",
            ordinary_weapon = "test"
        },
        parts = { old = "test", new = "test" }
    },
    Packages = { test = { _importSet = {} } }
}

dofile("Lua/Scripts/Gunsmith/Core.lua")
local Core = GunsmithFramework.Core
assert(Core.RequiresWorkbench(item("default_weapon")) == false)
assert(Core.RequiresWorkbench(item("workbench_weapon")) == true)

local actor = {}
local selection = { slot = "old" }
local consumed = 0
local saved = 0

Core.WeaponConfig = function(value) return value.weaponConfig end
Core.PlatformConfig = function() return {} end
Core.OwnerForWeapon = function() return "test" end
Core.ItemKey = function() return "weapon" end
Core.IsValidPath = function() return true end
Core.GetPart = function(partId) return GunsmithFramework.Config.parts[partId] end
Core.IsPartCompatible = function() return true end
Core.SortedSelectionPaths = function() return { "slot" } end
Core.PruneInvalidSelections = function() end
Core.ClearDescendants = function() end
Core.InvalidateQuickSlotsCache = function() end

GunsmithFramework.Persistence = {
    Save = function() saved = saved + 1 end
}
GunsmithFramework.Inventory = {
    ActorForItem = function() error("UiSpec must use its explicit actor") end,
    ItemIdentifierForPart = function(part) return part.item and part.item.identifier end,
    FindPartItem = function() return {} end,
    ConsumePartItem = function(actualActor)
        assert(actualActor == actor)
        consumed = consumed + 1
        return true
    end
}
GunsmithFramework.Stats = {
    Encode = function() return "" end,
    PartStats = function() return {} end,
    SumSelection = function() return {} end
}
GunsmithFramework.State = { selections = { weapon = selection }, loadedStates = { weapon = true } }
GunsmithFramework.QuickMod = {
    SlotForPath = function(_, path) return path == "quick" and 0 or nil end,
    SyncFromContainer = function(_, currentSelection, _, _, path)
        if path then currentSelection[path] = "new" end
        return false
    end,
    ClearSlot = function() return true end,
    CanSlotAcceptItem = function() return true end,
    InstallSpecificPartItem = function() return true end
}
GunsmithFramework.UiSpec = nil
SERVER = false

dofile("Lua/Scripts/Gunsmith/UiSpec.lua")
dofile("Lua/Scripts/Gunsmith/Runtime.lua")
GunsmithFramework.Runtime.Apply = function() end

local workbenchItem = item("workbench_weapon")
workbenchItem.weaponConfig = GunsmithFramework.Config.weapons.workbench_weapon
local workbench = {}
assert(GunsmithFramework.Runtime.SetPart(workbenchItem, "slot", "new", nil, actor) == nil)
assert(selection.slot == "old", "workbench-only weapons must reject ordinary writes")
assert(GunsmithFramework.Runtime.SetPartAtWorkbench(workbenchItem, workbench, actor, "slot", "new") == true)
assert(selection.slot == "new" and consumed == 1)

selection.quick = "old"
selection.slot = "old"
assert(GunsmithFramework.Runtime.SetPart(workbenchItem, "slot", "new", "quick", actor) == nil)
assert(selection.slot == "old", "quick mode must not bypass workbench policy for ordinary slots")
assert(GunsmithFramework.Runtime.SetPart(workbenchItem, "quick", "new", "quick", actor) == false)
assert(selection.quick == "new", "QuickUI quick slots must allow workbench-only weapons")

local ordinaryItem = item("ordinary_weapon")
ordinaryItem.weaponConfig = GunsmithFramework.Config.weapons.ordinary_weapon
selection.slot = "old"
assert(GunsmithFramework.Runtime.SetPart(ordinaryItem, "slot", "new", nil, actor, workbench) == nil)
assert(selection.slot == "old", "ordinary weapons must reject workbench writes")
assert(GunsmithFramework.Runtime.SetPart(ordinaryItem, "slot", "new", nil, actor) == true)
assert(selection.slot == "new")

GunsmithFramework.Core.EmptyPartForPath = function() return nil end
GunsmithFramework.Core.EncodePreview = function() return "" end
GunsmithFramework.Core.EncodeText = tostring
GunsmithFramework.Core.FrameworkLocalizationKey = function(key) return key end
GunsmithFramework.Core.GetPartsForType = function() return { "old", "new" } end
GunsmithFramework.Core.HasChildSlots = function() return false end
GunsmithFramework.Core.PartConflictsWithSelection = function() return false end
GunsmithFramework.Core.IsRequiredSlot = function() return false end
GunsmithFramework.Core.ItemIdentifier = function() return "workbench_weapon" end
GunsmithFramework.Core.NormalizeUiPath = function(_, path) return path end
GunsmithFramework.Core.OwnerForWeaponId = function() return "test" end
GunsmithFramework.Core.PathLabel = function() return "path" end
GunsmithFramework.Core.SlotsForPath = function()
    return { { path = "slot", partType = "slot", nameKey = "slot" } }
end
GunsmithFramework.Core.UiParentPath = function() return "" end
GunsmithFramework.Inventory.HasPartItem = function(actualActor)
    assert(actualActor == actor, "UiSpec must use the explicit actor")
    return true
end

local spec = GunsmithFramework.UiSpec.Build(workbenchItem, { slot = "old" }, {}, "", actor)
assert(spec:find("new:part.new:available", 1, true))
assert(saved > 0)
print("Workbench policy and explicit actor self-test passed")
