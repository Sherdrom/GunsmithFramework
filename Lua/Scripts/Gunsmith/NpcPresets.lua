GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = Gunsmith.Core
local Persistence = Gunsmith.Persistence
local QuickMod = Gunsmith.QuickMod
local Runtime = Gunsmith.Runtime
local Config = Gunsmith.Config

if Config then
    Config.npcPresets = Config.npcPresets or { profiles = {} }
    Config.npcPresets.profiles = Config.npcPresets.profiles or {}
end

local NpcPresets = {
    profiles = Config and Config.npcPresets.profiles or {}
}

Gunsmith.NpcPresets = NpcPresets

local appliedProfiles = setmetatable({}, { __mode = "k" })
local warned = {}

local function currentSavedState(item)
    if not Hook or not Hook.Call then return "" end
    local value = Hook.Call("GunsmithFrameworkGetSavedState", item)
    if type(value) == "string" then return value end
    return ""
end

local function warn(message)
    print("[GunsmithFramework][NpcPresets] " .. tostring(message))
end

local function warnOnce(key, message)
    if warned[key] then return end
    warned[key] = true
    warn(message)
end

local function canEnsureQuickPartItems()
    if not Hook or not Hook.Call then return false end
    if SERVER then return true end
    return Hook.Call("GunsmithFrameworkCanEnsureQuickPartItem") == true
end

local function xmlPresetName(item)
    if not Hook or not Hook.Call then return "" end
    local value = Hook.Call("GunsmithFrameworkGetNpcPreset", item)
    if type(value) == "string" then return value end
    return ""
end

local function matchingProfile(item, profileName)
    profileName = profileName or xmlPresetName(item)
    if profileName == "" then return nil end

    local profile = NpcPresets.profiles[profileName]
    if type(profile) ~= "table" then
        warnOnce("missing:" .. profileName, "Missing profile '" .. profileName .. "'.")
        return nil
    end

    local weaponIdentifier = Core.ItemIdentifier(item)
    if not weaponIdentifier then return nil end
    local weaponOwner = Core.OwnerForWeaponId(weaponIdentifier)
    local profileOwner = Core.OwnerForNpcPreset(profileName)
    if not Core.OwnerCanReference(weaponOwner, profileOwner) then
        warnOnce(
            "owner:" .. profileName .. ":" .. tostring(weaponIdentifier),
            "Profile '" .. tostring(profileName) .. "' is owned by package '" .. tostring(profileOwner or "<none>") .. "' and cannot be used by weapon '" .. tostring(weaponIdentifier) .. "' owned by '" .. tostring(weaponOwner or "<none>") .. "'.")
        return nil
    end
    if profile.weapon and profile.weapon ~= weaponIdentifier then
        warnOnce(
            "weapon:" .. profileName .. ":" .. tostring(weaponIdentifier),
            "Profile '" .. profileName .. "' expects weapon '" .. tostring(profile.weapon) .. "' but XML item is '" .. tostring(weaponIdentifier) .. "'.")
        return nil
    end

    return profileName, profile
end

local function validatePartPath(selection, platform, path, partId, ownerId)
    if not Core.IsValidPath(selection, platform, path) then
        warn("Invalid path '" .. tostring(path) .. "' for part '" .. tostring(partId) .. "'.")
        return false
    end
    local part = Gunsmith.Config.parts[partId]
    if not part then
        warn("Missing part '" .. tostring(partId) .. "'.")
        return false
    end
    if not Core.IsPartCompatible(selection, platform, path, partId, ownerId) then
        warn("Incompatible part '" .. tostring(partId) .. "' at path '" .. tostring(path) .. "'.")
        return false
    end
    return true
end

local function buildSelection(profile, platform, weapon, ownerId)
    local selection = Core.BuildDefaultSelection(platform, weapon, ownerId)
    local parts = type(profile.parts) == "table" and profile.parts or {}

    for path, partId in pairs(parts) do
        validatePartPath(selection, platform, path, partId, ownerId)
    end

    Persistence.ApplySavedParts(selection, platform, weapon, parts, ownerId)
    Core.PruneInvalidSelections(selection, platform, weapon, ownerId)
    return selection
end

local function ensureQuickPartItems(item, selection, platform, profileName)
    if not QuickMod or not canEnsureQuickPartItems() then return false end

    local ensuredAny = false

    for path, partId in pairs(selection) do
        local slotIndex = QuickMod.SlotForPath(item, path)
        if slotIndex ~= nil then
            local part = Gunsmith.Config.parts[partId]
            local identifier = part and part.item and part.item.identifier or nil
            if identifier and identifier ~= "" then
                local ok = Hook.Call("GunsmithFrameworkEnsureQuickPartItem", item, slotIndex, identifier)
                if ok ~= true then
                    warn("Failed to ensure quick-slot item '" .. tostring(identifier) .. "' for profile '" .. tostring(profileName) .. "'.")
                else
                    ensuredAny = true
                end
            else
                warn("Quick-slot part '" .. tostring(partId) .. "' has no item identifier.")
            end
        end
    end

    return ensuredAny
end

local function schedulePresetRecovery(item, profileName)
    if not Timer or not Timer.Wait then return end

    Timer.Wait(function()
        if not item or item.removed then return end
        if appliedProfiles[item] ~= profileName then return end

        local profile = NpcPresets.profiles[profileName]
        local State = Gunsmith.State
        local platform = Core.PlatformConfig(item)
        local weapon = Core.WeaponConfig(item)
        local key = Core.ItemKey(item)
        if type(profile) ~= "table" or not State or not platform or not weapon or not key then return end

        local selection = buildSelection(profile, platform, weapon, Core.OwnerForWeapon(weapon))
        State.selections[key] = selection
        State.loadedStates[key] = true
        State.appliedSignatures[item] = nil
        State.appliedConfigSignatures[item] = nil

        if Runtime then
            Runtime.Apply(item, true)
            Runtime.RefreshParts(item, true)
            Runtime.RefreshQuick(item, true)
        end
    end, 250)
end

function NpcPresets.TryApply(item, diagnose, presetName)
    if not item or item.removed then return false end

    local profileName = type(presetName) == "string" and presetName or ""

    local weapon = Core.WeaponConfig(item)
    if not weapon then
        if diagnose then
            if profileName == "" then
                profileName = xmlPresetName(item)
            end
            warnOnce(
                "weaponconfig:" .. profileName .. ":" .. tostring(Core.ItemIdentifier(item)),
                "Profile '" .. tostring(profileName) .. "' is bound to '" .. tostring(Core.ItemIdentifier(item)) .. "', but that item has no Gunsmith weapon config.")
        end
        return false
    end

    if profileName == "" then
        profileName = xmlPresetName(item)
    end
    if profileName == "" then return false end

    local profileName, profile = matchingProfile(item, profileName)
    if not profile then return false end

    if appliedProfiles[item] == profileName then
        if Runtime then Runtime.Apply(item, true) end
        return true
    end

    if SERVER and currentSavedState(item) ~= "" then
        warnOnce(
            "saved:" .. profileName .. ":" .. tostring(Core.ItemIdentifier(item)),
            "Skipped profile '" .. tostring(profileName) .. "' for '" .. tostring(Core.ItemIdentifier(item)) .. "' because it already has saved Gunsmith state.")
        return false
    end

    local State = Gunsmith.State
    local platform = Core.PlatformConfig(item)
    local key = Core.ItemKey(item)
    local ownerId = Core.OwnerForWeapon(weapon)
    if not State or not platform or not weapon or not key then
        warnOnce(
            "runtime:" .. profileName .. ":" .. tostring(Core.ItemIdentifier(item)),
            "Profile '" .. tostring(profileName) .. "' for '" .. tostring(Core.ItemIdentifier(item)) .. "' could not apply because Gunsmith runtime state is incomplete.")
        return false
    end

    local selection = buildSelection(profile, platform, weapon, ownerId)
    State.selections[key] = selection
    State.loadedStates[key] = true
    State.appliedSignatures[item] = nil
    State.appliedConfigSignatures[item] = nil
    appliedProfiles[item] = profileName

    local ensuredQuickItems = ensureQuickPartItems(item, selection, platform, profileName)

    if SERVER and Persistence then
        Persistence.Save(item)
    end
    if Runtime then
        Runtime.Apply(item, true)
    end
    if ensuredQuickItems then
        schedulePresetRecovery(item, profileName)
    end
    return true
end
