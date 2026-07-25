CLIENT = true

local command
local requested
local messages = {}
local item = { ID = 42 }

GunsmithFramework = {
    Core = {
        ItemIdentifier = function() return "deep_hk416" end,
        ItemKey = function() return "42" end,
        SortedSelectionPaths = function() return { "handguard" } end
    },
    Persistence = {
        Request = function(value) requested = value end
    },
    Runtime = {
        SelectedHandWeapon = function() return item end
    },
    State = {
        selections = { ["42"] = { handguard = "deep_hk416_14mrs_handguard" } },
        loadedStates = { ["42"] = true }
    }
}

Character = { Controlled = {} }
Game = {
    AddCommand = function(_, _, callback) command = callback end
}
Hook = {
    Call = function(name)
        if name == "GunsmithFrameworkGetSavedState" then return "saved-14mrs" end
        if name == "GunsmithFrameworkDebugSpriteState" then return "layers=handguard=14mrs" end
    end
}
Timer = { Wait = function(callback, delay) assert(delay == 250) callback() end }
print = function(message) table.insert(messages, message) end

dofile("Lua/Scripts/Gunsmith/Debug.lua")
GunsmithFramework.Debug.RegisterCommands()
command()

assert(requested == item)
assert(#messages == 8)
assert(messages[2]:find("saved%-14mrs"))
assert(messages[3]:find("deep_hk416_14mrs_handguard", 1, true))
assert(messages[4]:find("layers=handguard=14mrs", 1, true))
