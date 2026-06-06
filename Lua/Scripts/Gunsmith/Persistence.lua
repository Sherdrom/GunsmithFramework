GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = Gunsmith.Core
local Persistence = {}
Gunsmith.Persistence = Persistence

local saveVersion = 1

local function jsonEscape(value)
    return tostring(value):gsub('[\\"]', {
        ["\\"] = "\\\\",
        ["\""] = "\\\""
    })
end

local function jsonUnescape(value)
    return tostring(value):gsub("\\(.)", function(escaped)
        if escaped == "\\" then return "\\" end
        if escaped == "\"" then return "\"" end
        if escaped == "/" then return "/" end
        if escaped == "n" then return "\n" end
        if escaped == "r" then return "\r" end
        if escaped == "t" then return "\t" end
        return escaped
    end)
end

local function sortedSavedPaths(savedParts)
    local paths = {}
    for path, _ in pairs(savedParts) do
        table.insert(paths, path)
    end
    table.sort(paths, function(left, right)
        local _, leftDepth = string.gsub(left, "/", "")
        local _, rightDepth = string.gsub(right, "/", "")
        if leftDepth == rightDepth then return left < right end
        return leftDepth < rightDepth
    end)
    return paths
end

function Persistence.Encode(selection, platform, weapon)
    local entries = {}

    for _, root in ipairs(Core.RootSlotDefs(platform)) do
        local path = root.path
        local partId = selection[path]
        table.insert(entries, string.format("\"%s\":\"%s\"", jsonEscape(path), jsonEscape(partId or Gunsmith.EmptyPartId)))
    end

    for _, path in ipairs(Core.SortedSelectionPaths(selection)) do
        if not Core.IsRootSlot(platform, path) and selection[path] then
            table.insert(entries, string.format("\"%s\":\"%s\"", jsonEscape(path), jsonEscape(selection[path])))
        end
    end

    return string.format("{\"v\":%d,\"parts\":{%s}}", saveVersion, table.concat(entries, ","))
end

function Persistence.Decode(json)
    if type(json) ~= "string" or json == "" then return nil end

    local partsText = string.match(json, '"parts"%s*:%s*{(.-)}')
    if not partsText then return nil end

    local parts = {}
    for rawPath, rawPartId in string.gmatch(partsText, '"([^"]*)"%s*:%s*"([^"]*)"') do
        local path = jsonUnescape(rawPath)
        local partId = jsonUnescape(rawPartId)
        if path ~= "" and partId ~= "" then
            parts[path] = partId
        end
    end

    return parts
end

function Persistence.ApplySavedParts(selection, platform, weapon, savedParts, ownerId)
    if type(savedParts) ~= "table" then return end
    ownerId = ownerId or Core.OwnerForWeapon(weapon)

    for _, path in ipairs(sortedSavedPaths(savedParts)) do
        local partId = savedParts[path]
        if Core.IsValidPath(selection, platform, path) then
            if partId == Gunsmith.EmptyPartId then
                if not Core.IsRequiredSlot(platform, path) then
                    selection[path] = nil
                end
            else
                local part = Gunsmith.Config.parts[partId]
                if part and Core.IsPartCompatible(selection, platform, path, partId, ownerId) then
                    selection[path] = partId
                end
            end
            Core.PruneInvalidSelections(selection, platform, weapon, ownerId)
        end
    end
end

function Persistence.Receive(item, json)
    local State = Gunsmith.State
    local platform = Core.PlatformConfig(item)
    local weapon = Core.WeaponConfig(item)
    local key = Core.ItemKey(item)
    if not State or not platform or not key then return end

    local hasSavedState = type(json) == "string" and json ~= ""
    local ownerId = Core.OwnerForWeapon(weapon)
    local selection = Core.BuildDefaultSelection(platform, weapon, ownerId)
    Persistence.ApplySavedParts(selection, platform, weapon, Persistence.Decode(json), ownerId)
    Core.PruneInvalidSelections(selection, platform, weapon, ownerId)

    State.selections[key] = selection
    State.loadedStates[key] = true
    State.appliedSignatures[item] = nil
    State.appliedConfigSignatures[item] = nil

    if Gunsmith.Runtime then
        Gunsmith.Runtime.Apply(item, hasSavedState)
    end
end

function Persistence.Request(item)
    if not Hook or not Hook.Call then return end
    Hook.Call("GunsmithFrameworkRequestState", item)
end

function Persistence.Save(item)
    if not Hook or not Hook.Call then return end
    local platform = Core.PlatformConfig(item)
    local weapon = Core.WeaponConfig(item)
    local selection = Gunsmith.Runtime and Gunsmith.Runtime.GetSelection(item) or nil
    if not platform or not selection then return end

    Core.PruneInvalidSelections(selection, platform, weapon, Core.OwnerForWeapon(weapon))
    Hook.Call("GunsmithFrameworkSaveState", item, Persistence.Encode(selection, platform, weapon))
end
