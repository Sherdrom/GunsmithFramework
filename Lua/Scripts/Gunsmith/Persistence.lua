GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = Gunsmith.Core
local Persistence = {}
Gunsmith.Persistence = Persistence

local saveVersion = 1

local function encodeString(value)
    local encoded = json.serialize({ tostring(value) })
    return string.sub(encoded, 2, -2)
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
        table.insert(entries, encodeString(path) .. ":" .. encodeString(partId or Gunsmith.EmptyPartId))
    end

    local nestedParts = {}
    for path, partId in pairs(selection) do
        if not Core.IsRootSlot(platform, path) and partId then
            nestedParts[path] = partId
        end
    end

    local defaults = Core.BuildDefaultSelection(platform, weapon, Core.OwnerForWeapon(weapon))
    for path, _ in pairs(defaults) do
        if not Core.IsRootSlot(platform, path)
            and selection[path] == nil
            and not Core.IsRequiredSlot(platform, path)
            and Core.IsValidPath(selection, platform, path) then
            nestedParts[path] = Gunsmith.EmptyPartId
        end
    end

    for _, path in ipairs(Core.SortedSelectionPaths(nestedParts)) do
        table.insert(entries, encodeString(path) .. ":" .. encodeString(nestedParts[path]))
    end

    return string.format("{\"v\":%d,\"parts\":{%s}}", saveVersion, table.concat(entries, ","))
end

function Persistence.Decode(value)
    if type(value) ~= "string" or value == "" then return nil end

    local ok, data = pcall(json.parse, value)
    if not ok or type(data) ~= "table" or data.v ~= saveVersion or type(data.parts) ~= "table" then return nil end

    local parts = {}
    for path, partId in pairs(data.parts) do
        if type(path) == "string" and path ~= "" and type(partId) == "string" and partId ~= "" then
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
    if State.lastQuickSignatures then State.lastQuickSignatures[item] = nil end

    if Gunsmith.Runtime then
        Gunsmith.Runtime.Apply(item, hasSavedState)
        Gunsmith.Runtime.RefreshParts(item, true)
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
