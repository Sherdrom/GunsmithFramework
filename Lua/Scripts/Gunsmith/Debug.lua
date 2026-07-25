GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = Gunsmith.Core
local Persistence = Gunsmith.Persistence
local Runtime = Gunsmith.Runtime
local Debug = {}
Gunsmith.Debug = Debug

local function selectionSpec(selection)
    if type(selection) ~= "table" then return "<none>" end
    local values = {}
    for _, path in ipairs(Core.SortedSelectionPaths(selection)) do
        table.insert(values, path .. "=" .. tostring(selection[path]))
    end
    return table.concat(values, ",")
end

local function dumpState(item, label)
    if not item then
        print("[GunsmithFramework][Debug][" .. label .. "] Hold a Gunsmith weapon first.")
        return
    end

    local state = Gunsmith.State or {}
    local key = Core.ItemKey(item)
    print(string.format(
        "[GunsmithFramework][Debug][%s] item=%s id=%s key=%s loaded=%s",
        label,
        tostring(Core.ItemIdentifier(item)),
        tostring(item.ID),
        tostring(key),
        tostring(state.loadedStates and state.loadedStates[key])))
    print("[GunsmithFramework][Debug][" .. label .. "] saved=" ..
        tostring(Hook.Call("GunsmithFrameworkGetSavedState", item)))
    print("[GunsmithFramework][Debug][" .. label .. "] selection=" ..
        selectionSpec(state.selections and state.selections[key]))
    print("[GunsmithFramework][Debug][" .. label .. "] sprite=" ..
        tostring(Hook.Call("GunsmithFrameworkDebugSpriteState", item)))
end

function Debug.RegisterCommands()
    if not CLIENT or Debug.CommandsRegistered or not Game or not Game.AddCommand then return end
    Debug.CommandsRegistered = true

    Game.AddCommand("GunsmithFrameworkDebugState", "Print saved, selected, and rendered state for the held Gunsmith weapon", function()
        local item = Runtime.SelectedHandWeapon(Character and Character.Controlled)
        dumpState(item, "before")
        if not item then return end

        Persistence.Request(item)
        if Timer and Timer.Wait then
            Timer.Wait(function() dumpState(item, "after") end, 250)
        else
            dumpState(item, "after")
        end
    end, nil, false)
end
