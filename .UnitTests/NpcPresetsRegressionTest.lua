local function fixture(mode)
    CLIENT, SERVER = mode ~= "server", mode == "server"
    Game = { IsMultiplayer = mode ~= "singleplayer" }
    local parts = {
        receiver = {
            type = "receiver",
            mounts = {
                { path = "optic", accepts = { "optic" } },
                { path = "carry_handle", accepts = { "handle" }, defaultPart = "handle" }
            }
        },
        custom_receiver = { type = "receiver", stats = { Ergonomics = 10 } },
        handle = {
            type = "carry_handle", provides = { "handle" },
            mounts = { { path = "optic", accepts = { "optic" } } }
        },
        scope = {
            type = "optic", provides = { "optic" }, item = { identifier = "scope_item" },
            mounts = { { path = "reticle", accepts = { "reticle" } } }
        },
        reticle = { type = "reticle", provides = { "reticle" }, item = { virtual = true }, stats = { Ergonomics = 2 } },
        long_barrel = { type = "barrel", provides = { "barrel" } },
        short_barrel = { type = "barrel", provides = { "barrel" }, groups = { "short_barrel" } },
        long_handguard = { type = "handguard", provides = { "handguard" }, groups = { "long_handguard" } },
        short_handguard = { type = "handguard", provides = { "handguard" } },
        foreign_barrel = { type = "barrel", provides = { "barrel" } }
    }
    local platform = {
        canvas = { w = 100, h = 100 },
        rootSlots = { { path = "receiver" }, { path = "barrel" }, { path = "handguard" } }
    }
    local weapon = {
        platform = "test",
        roots = { receiver = { part = "receiver" }, barrel = { part = "long_barrel" }, handguard = { part = "long_handguard" } },
        quickSlots = { { path = "receiver/optic", slot = 0 }, { path = "receiver/carry_handle/optic", slot = 0 } }
    }
    local profile = {
        weapon = "rifle",
        parts = { ["receiver/carry_handle/optic"] = "scope", ["receiver/carry_handle/optic/reticle"] = "reticle" }
    }
    GunsmithFramework = {
        Config = {
            parts = parts, platforms = { test = platform }, weapons = { rifle = weapon },
            exclusiveGroups = { short_barrel = { "long_handguard" } },
            npcPresets = { profiles = { guard = profile } }
        },
        Owners = { parts = {}, platforms = { test = "test" }, weapons = { rifle = "test" }, npcPresets = { guard = "test" } },
        Packages = { test = { _importSet = {} } }
    }
    for id in pairs(parts) do GunsmithFramework.Owners.parts[id] = "test" end
    GunsmithFramework.Owners.parts.foreign_barrel = "foreign"

    local inventory = { items = {} }
    inventory.GetItemsAt = function(slot)
        assert(slot == 0)
        local index = 0
        return function() index = index + 1; return inventory.items[index] end
    end
    local item = { ID = 1, Prefab = { Identifier = { Value = "rifle" } }, OwnInventory = inventory }
    local context = { item = item, weapon = weapon, platform = platform, profile = profile, saved = "", saves = 0, broadcasts = 0, ensures = 0, timers = {}, applies = 0 }
    Timer = { Wait = function(callback, delay) table.insert(context.timers, { callback = callback, delay = delay }) end }
    Hook = {
        Call = function(name, _, value)
            if name == "GunsmithFrameworkGetNpcPreset" then return "guard" end
            if name == "GunsmithFrameworkGetSavedState" then return context.saved end
            if name == "GunsmithFrameworkCanEnsureQuickPartItem" then return mode ~= "client" end
            if name == "GunsmithFrameworkEnsureQuickPartItem" then context.ensures = context.ensures + 1; return true end
            if name == "GunsmithFrameworkSaveState" and mode ~= "client" then
                context.saved = value
                context.saves = context.saves + 1
                if SERVER then context.broadcasts = context.broadcasts + 1 end
            end
            if name == "GunsmithFrameworkApply" or name == "GunsmithFrameworkApplyRuntimeState" then
                context.applies = context.applies + 1
                context.signature = value
                return true
            end
            return false
        end
    }
    -- These fixtures use simple ASCII IDs; LuaCs supplies the real JSON implementation.
    json = {
        serialize = function(values) return '["' .. values[1] .. '"]' end,
        parse = function(value)
            local savedParts = {}
            for path, part in value:gmatch('"([^"]+)":"([^"]+)"') do savedParts[path] = part end
            return { v = 1, parts = savedParts }
        end
    }
    for _, name in ipairs({ "Core", "Stats", "Persistence", "QuickMod", "Runtime", "NpcPresets" }) do
        dofile("Lua/Scripts/Gunsmith/" .. name .. ".lua")
    end
    context.g = GunsmithFramework
    context.selection = function() return context.g.Runtime.GetSelection(item) end
    context.receive = function(parts)
        context.saved = context.g.Persistence.Encode(parts, platform, weapon)
        context.g.Persistence.Receive(item, context.saved)
    end
    context.flush = function()
        local pending = context.timers
        context.timers = {}
        for _, timer in ipairs(pending) do timer.callback() end
    end
    return context
end

local tests = {}

tests["multiplayer inventory delivery preserves authoritative mount and children"] = function()
    local c = fixture("client")
    local selection = c.g.Core.BuildDefaultSelection(c.platform, c.weapon, "test")
    for path, part in pairs(c.profile.parts) do selection[path] = part end
    c.receive(selection)
    c.g.Runtime.SyncQuickModContainerItem(c.item)
    c.g.Runtime.SyncQuickContainer(c.item)
    c.g.Runtime.Apply(c.item)
    c.item.OwnInventory.items = { { Prefab = { Identifier = { Value = "scope_item" } } } }
    c.g.Runtime.SyncQuickModContainerItem(c.item)
    c.flush()
    assert(c.selection()["receiver/carry_handle/optic/reticle"] == "reticle", "late attachment removed the authoritative child")
    assert(c.selection()["receiver/carry_handle/optic"] == "scope", "late attachment changed the authoritative mount")
    assert(c.selection()["receiver/optic"] == nil)
    assert(c.signature:find("Ergonomics=2.0000", 1, true))
    assert(c.ensures == 0 and c.saves == 0)

    c.item.OwnInventory.items = {}
    c.g.Runtime.SyncQuickModContainerItem(c.item)
    assert(c.selection()["receiver/carry_handle/optic"] == "scope", "client must wait for the removal snapshot")
    c.receive(c.g.Core.BuildDefaultSelection(c.platform, c.weapon, "test"))
    assert(c.selection()["receiver/carry_handle/optic"] == nil, "authoritative removal must still apply")
end

tests["multiplayer client never applies an NPC preset locally"] = function()
    local c = fixture("client")
    assert(not c.g.NpcPresets.TryApply(c.item, true, "guard"))
    assert(c.g.State.loadedStates["1"] ~= true and c.saves == 0 and c.ensures == 0)
end

tests["singleplayer preset survives a saved-state reload without editing"] = function()
    local c = fixture("singleplayer")
    c.weapon.quickSlots = {}
    c.profile.parts = { receiver = "custom_receiver" }
    assert(c.g.NpcPresets.TryApply(c.item, true, "guard"))
    assert(c.saves == 1, "singleplayer preset was never saved")
    c.g.State.selections["1"], c.g.State.loadedStates["1"] = nil, nil
    assert(c.selection().receiver == "custom_receiver")
    assert(c.broadcasts == 0)
end

tests["existing saved state wins over the preset in both authority modes"] = function()
    for _, mode in ipairs({ "singleplayer", "server" }) do
        local c = fixture(mode)
        local saved = c.g.Core.BuildDefaultSelection(c.platform, c.weapon, "test")
        saved.receiver = "custom_receiver"
        c.saved = c.g.Persistence.Encode(saved, c.platform, c.weapon)
        assert(not c.g.NpcPresets.TryApply(c.item, true, "guard"))
        assert(c.selection().receiver == "custom_receiver" and c.saves == 0)
    end
end

tests["delayed refresh preserves edits and the published state"] = function()
    for _, mode in ipairs({ "singleplayer", "server" }) do
        local c = fixture(mode)
        assert(c.g.NpcPresets.TryApply(c.item, true, "guard"))
        assert(#c.timers > 0)
        c.selection().receiver = "custom_receiver"
        c.g.Persistence.Save(c.item)
        local saved, saves, broadcasts = c.saved, c.saves, c.broadcasts
        c.flush()
        assert(c.selection().receiver == "custom_receiver", "delayed recovery overwrote a newer edit")
        assert(c.saved == saved and c.saves == saves and c.broadcasts == broadcasts)
        assert(c.g.Persistence.Encode(c.selection(), c.platform, c.weapon) == c.saved)
        assert(c.signature:find("Ergonomics=10.0000", 1, true))
    end
end

tests["delayed refresh still reapplies an unchanged preset and skips removed items"] = function()
    local c = fixture("server")
    assert(c.g.NpcPresets.TryApply(c.item, true, "guard"))
    local applies, saved = c.applies, c.saved
    c.item.OwnInventory.items = { { Prefab = { Identifier = { Value = "scope_item" } } } }
    c.flush()
    assert(c.applies > applies and c.saved == saved)
    assert(c.selection()["receiver/carry_handle/optic/reticle"] == "reticle")
    c = fixture("server")
    assert(c.g.NpcPresets.TryApply(c.item, true, "guard"))
    applies = c.applies
    c.item.removed = true
    c.flush()
    assert(c.applies == applies)
end

tests["valid batch replacements are independent of the old default conflicts"] = function()
    local c = fixture("server")
    c.profile.parts = { barrel = "short_barrel", handguard = "short_handguard" }
    local defaults = c.g.Core.BuildDefaultSelection(c.platform, c.weapon, "test")
    assert(not c.g.Core.IsPartCompatible(defaults, c.platform, "barrel", "short_barrel", "test"), "interactive checks must still reject conflicts")
    assert(c.g.NpcPresets.TryApply(c.item, true, "guard"))
    assert(c.selection().barrel == "short_barrel" and c.selection().handguard == "short_handguard", "default part blocked the valid target preset")
    c.g.Persistence.Receive(c.item, c.saved)
    assert(c.selection().barrel == "short_barrel" and c.selection().handguard == "short_handguard", "saved-state replay must match preset application")
end

tests["batch restore keeps ownership, type, empty and final conflict validation"] = function()
    local c = fixture("server")
    local core, persistence = c.g.Core, c.g.Persistence
    local selection = core.BuildDefaultSelection(c.platform, c.weapon, "test")
    persistence.ApplySavedParts(selection, c.platform, c.weapon, { barrel = "foreign_barrel", handguard = "scope", missing = "scope" }, "test")
    assert(selection.barrel == "long_barrel" and selection.handguard == "long_handguard" and selection.missing == nil)
    persistence.ApplySavedParts(selection, c.platform, c.weapon, { barrel = "short_barrel", handguard = "__empty" }, "test")
    assert(selection.barrel == "short_barrel" and selection.handguard == nil)
    persistence.ApplySavedParts(selection, c.platform, c.weapon, { barrel = "short_barrel", handguard = "long_handguard" }, "test")
    assert(not (selection.barrel == "short_barrel" and selection.handguard == "long_handguard"))
    for path, part in pairs(selection) do assert(core.IsPartCompatible(selection, c.platform, path, part, "test")) end
end

tests["server and singleplayer still reconcile real attachment removal"] = function()
    for _, mode in ipairs({ "singleplayer", "server" }) do
        local c = fixture(mode)
        assert(c.g.NpcPresets.TryApply(c.item, true, "guard"))
        c.g.Runtime.SyncQuickModContainerItem(c.item)
        assert(c.selection()["receiver/carry_handle/optic"] == nil)
        assert(c.selection()["receiver/carry_handle/optic/reticle"] == nil)
        assert(c.saved == c.g.Persistence.Encode(c.selection(), c.platform, c.weapon))
    end
end

local failures, count = {}, 0
for name, test in pairs(tests) do
    count = count + 1
    local ok, err = pcall(test)
    if not ok then failures[#failures + 1] = name .. ": " .. tostring(err) end
end
assert(#failures == 0, table.concat(failures, "\n"))
print("NPC preset regressions passed: " .. count .. " cases")
