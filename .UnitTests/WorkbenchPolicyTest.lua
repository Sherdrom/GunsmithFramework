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
    CanSaveSelection = function() return true end,
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
assert(GunsmithFramework.Runtime.SetPartAtWorkbench(ordinaryItem, workbench, actor, "slot", "new") == true)
assert(selection.slot == "new", "ordinary weapons must also accept workbench writes")
selection.slot = "old"
assert(GunsmithFramework.Runtime.SetPart(ordinaryItem, "slot", "new", nil, actor) == true)
assert(selection.slot == "new")

GunsmithFramework.QuickMod.PartIdForItem = function() return "new" end
GunsmithFramework.Persistence.CanSaveSelection = function() return false end
assert(GunsmithFramework.Runtime.InstallQuickItem(ordinaryItem, "quick", {}) == false,
    "oversized quick-drag changes must fail before inventory access")
GunsmithFramework.Persistence.CanSaveSelection = function() return true end

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
GunsmithFramework.Inventory.CollectAvailableItemIds = function(actualActor, sourceItem)
    assert(actualActor == actor, "UiSpec must use the explicit actor")
    assert(sourceItem == workbenchItem, "UiSpec must exclude the weapon inventory")
    return {}
end

local spec = GunsmithFramework.UiSpec.Build(workbenchItem, { slot = "old" }, {}, "", actor)
assert(spec:find("new:part.new:available", 1, true))
assert(saved > 0)

SERVER = true
GunsmithFramework.Persistence.Encode = function() return "encoded" end
selection.slot = "old"
assert(GunsmithFramework.Runtime.SetPartFromClient(workbenchItem, actor, "slot", "new") == false)
assert(selection.slot == "old", "server must reject workbench-only changes without a workbench")
assert(GunsmithFramework.Runtime.SetPartFromClient(workbenchItem, actor, "slot", "new", workbench) == "encoded")
selection.slot = "old"
assert(GunsmithFramework.Runtime.SetPartFromClient(ordinaryItem, actor, "slot", "new", workbench) == "encoded")
selection.slot = "old"
assert(GunsmithFramework.Runtime.SetPartFromClient(ordinaryItem, actor, "slot", "new") == "encoded")
assert(GunsmithFramework.Runtime.SetPartFromClient(ordinaryItem, actor, "quick", "new", workbench) == false)

CLIENT = true
SERVER = false
local callbacks = {}
Hook = {
    Add = function(name, _, callback) callbacks[name] = callback end,
    Patch = function() end,
    HookMethodType = { After = 1 }
}
LuaUserData = {
    IsTargetType = function(value, typeName)
        return typeName == "Barotrauma.Item" and type(value) == "table" and value.Prefab ~= nil
    end
}
dofile("Lua/Scripts/Gunsmith/Hooks.lua")
GunsmithFramework.Hooks.Register()
assert(callbacks.GunsmithFrameworkCanOpenFromWorkbench(ordinaryItem))
assert(callbacks.GunsmithFrameworkCanOpenFromWorkbench(workbenchItem))
assert(not callbacks.GunsmithFrameworkCanOpenFromWorkbench({}))
assert(not callbacks.GunsmithFrameworkCanOpenFromWorkbench(nil))

local calls = {}
Hook.Call = function(name)
    if name == "GunsmithFrameworkIsOpen" then return true end
    table.insert(calls, name)
end
GunsmithFramework.UiSpec.Build = function(_, _, _, path, actualActor)
    assert(actualActor == actor)
    return path
end
assert(GunsmithFramework.Runtime.OpenAtWorkbench(ordinaryItem, workbench, actor))
assert(GunsmithFramework.Runtime.OpenAtWorkbench(workbenchItem, workbench, actor))
assert(table.concat(calls, ",") == "GunsmithFrameworkOpenAtWorkbench,GunsmithFrameworkOpenAtWorkbench")
GunsmithFramework.Runtime.Open(workbenchItem)
assert(#calls == 2, "workbench-only weapons must not open through the ordinary shortcut")
callbacks.GunsmithFrameworkEnterPath(workbenchItem, "slot")
assert(calls[3] == "GunsmithFrameworkRefreshParts", "navigation must retain the current workbench window")
assert(GunsmithFramework.Runtime.GetCurrentUiPath(workbenchItem) == "slot")
print("Workbench policy and explicit actor self-test passed")

GunsmithFramework.Config = {
    platforms = { grouped = {
        rootSlots = { { path = "receiver", hidden = true } },
        pathNameKeys = { optic_mount = "test.category.optics" }
    } },
    weapons = { grouped_weapon = { platform = "grouped", roots = { receiver = { part = "receiver" } } } },
    parts = {
        receiver = { type = "receiver", mounts = {
            { path = "top", partType = "optic_mount", accepts = { "scope" } },
            { path = "muzzle_mount", accepts = { "muzzle" } }
        } },
        scope = { type = "optic_mount", provides = { "scope" }, item = { identifier = "scope:item" },
            mounts = { { path = "adapter", accepts = { "adapter" } } } },
        scope_duplicate = { type = "z_other", provides = { "scope" }, item = { identifier = "scope:item" } },
        adapter = { type = "adapter", provides = { "adapter" }, item = { identifier = "adapter_item" },
            mounts = { { path = "optic_mount", accepts = { "scope" } } } },
        muzzle = { type = "muzzle_mount", provides = { "muzzle" }, item = { identifier = "muzzle_item" } },
        foreign_scope = { type = "optic_mount", provides = { "scope" }, item = { identifier = "foreign_item" } }
    }
}
GunsmithFramework.Owners = {
    platforms = { grouped = "test" }, weapons = { grouped_weapon = "test" },
    parts = { receiver = "test", scope = "test", scope_duplicate = "test", adapter = "test", muzzle = "test", foreign_scope = "foreign" }
}
GunsmithFramework.Packages = { test = { _importSet = {}, localizationPrefix = "test" } }
dofile("Lua/Scripts/Gunsmith/Core.lua")
dofile("Lua/Scripts/Gunsmith/Runtime.lua")
dofile("Lua/Scripts/Gunsmith/Hooks.lua")
GunsmithFramework.Hooks.Register()
local groupedItem = item("grouped_weapon")
assert(callbacks.GunsmithFrameworkGetFabricatorPartItemIds(groupedItem) == "adapter_item,muzzle_item,scope:item")
assert(callbacks.GunsmithFrameworkGetFabricatorPartItemIds(groupedItem, true) ==
    "adapter_item:adapter:test.path.adapter|muzzle_item:muzzle_mount:test.path.muzzle_mount|scope%3Aitem:optic_mount:test.category.optics",
    "categories must reuse compatible traversal, localize types, deduplicate items and preserve owner isolation")
assert(GunsmithFramework.Runtime.FabricatorPartItemIds(nil, true) == "")
print("Workbench part category self-test passed")
