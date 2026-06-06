GunsmithFramework = GunsmithFramework or {}

local Framework = GunsmithFramework
Framework.Name = Framework.Name or "GunsmithFramework"
Framework.ApiVersion = Framework.ApiVersion or 1
Framework.LocalizationPrefix = Framework.LocalizationPrefix or "gunsmith.framework"
Framework.LocalizationFiles = Framework.LocalizationFiles or { "text/English.xml", "text/Chinese.xml" }
Framework.Config = Framework.Config or {}
Framework.Config.platforms = Framework.Config.platforms or {}
Framework.Config.weapons = Framework.Config.weapons or {}
Framework.Config.parts = Framework.Config.parts or {}
Framework.Config.npcPresets = Framework.Config.npcPresets or { profiles = {} }
Framework.Config.npcPresets.profiles = Framework.Config.npcPresets.profiles or {}
Framework.Packages = Framework.Packages or {}
Framework.PackageOrder = Framework.PackageOrder or {}
Framework.Owners = Framework.Owners or {
    platforms = {},
    weapons = {},
    parts = {},
    npcPresets = {}
}

local basePath = Framework.ScriptPath
    or (Framework.ModDir and (Framework.ModDir .. "/Lua/Scripts/Gunsmith"))
    or (Deep_Lua and Deep_Lua.Path and (Deep_Lua.Path .. "/Lua/Scripts/Gunsmith"))

if not basePath then
    error("[GunsmithFramework] ScriptPath is not configured.")
end

Framework.ScriptPath = basePath

local function packageLabel(package)
    if not package then return "<unknown>" end
    return tostring(package.id or package.name or "<unknown>")
end

local function ensurePackage(package)
    if type(package) ~= "table" then
        error("[GunsmithFramework] package must be a table.")
    end
    if type(package.id) ~= "string" or package.id == "" then
        error("[GunsmithFramework] package.id is required.")
    end
    if type(package.modDir) ~= "string" or package.modDir == "" then
        error("[GunsmithFramework] package.modDir is required for package '" .. packageLabel(package) .. "'.")
    end
    package.weaponTags = package.weaponTags or {}
    package.partTags = package.partTags or {}
    package.localizationFiles = package.localizationFiles or {}
    package.localizationPrefix = package.localizationPrefix or Framework.LocalizationPrefix
    return package
end

local function rememberPackage(package)
    if not Framework.Packages[package.id] then
        table.insert(Framework.PackageOrder, package.id)
    end
    Framework.Packages[package.id] = package
end

local function registerOwned(kind, key, value, package, override)
    if type(key) ~= "string" or key == "" then
        error("[GunsmithFramework] " .. kind .. " id must be a non-empty string.")
    end
    local bucket = Framework.Config[kind]
    local owners = Framework.Owners[kind]
    local previousOwner = owners[key]
    if bucket[key] ~= nil and previousOwner ~= package.id and not override then
        error(string.format(
            "[GunsmithFramework] duplicate %s '%s' from package '%s'; already owned by '%s'.",
            kind,
            key,
            packageLabel(package),
            tostring(previousOwner or "<unknown>")))
    end
    bucket[key] = value
    owners[key] = package.id
end

function Framework.RegisterPlatform(package, id, definition)
    package = ensurePackage(package)
    registerOwned("platforms", id, definition, package, package.override == true)
end

function Framework.RegisterWeapon(package, identifier, definition)
    package = ensurePackage(package)
    registerOwned("weapons", identifier, definition, package, package.override == true)
end

function Framework.RegisterPart(package, id, definition)
    package = ensurePackage(package)
    registerOwned("parts", id, definition, package, package.override == true)
end

function Framework.RegisterNpcPreset(package, id, definition)
    package = ensurePackage(package)
    Framework.Config.npcPresets = Framework.Config.npcPresets or { profiles = {} }
    Framework.Config.npcPresets.profiles = Framework.Config.npcPresets.profiles or {}
    local owners = Framework.Owners.npcPresets
    local profiles = Framework.Config.npcPresets.profiles
    local previousOwner = owners[id]
    if profiles[id] ~= nil and previousOwner ~= package.id and package.override ~= true then
        error(string.format(
            "[GunsmithFramework] duplicate npc preset '%s' from package '%s'; already owned by '%s'.",
            id,
            packageLabel(package),
            tostring(previousOwner or "<unknown>")))
    end
    profiles[id] = definition
    owners[id] = package.id
end

function Framework.CurrentPackage()
    return Framework._currentPackage
end

local function snapshotEntries(tableValue)
    local entries = {}
    for key, value in pairs(tableValue or {}) do
        entries[key] = value
    end
    return entries
end

local function claimLegacyEntry(kind, key, value, previousValue, package)
    local owners = Framework.Owners[kind]
    local previousOwner = owners[key]
    if previousValue ~= nil and value ~= previousValue and previousOwner ~= package.id and package.override ~= true then
        error(string.format(
            "[GunsmithFramework] duplicate %s '%s' from package '%s'; already owned by '%s'.",
            kind,
            key,
            packageLabel(package),
            tostring(previousOwner or "<unknown>")))
    end
    if previousValue == nil or value ~= previousValue or previousOwner == package.id then
        owners[key] = package.id
    end
end

local function assignLegacyOwners(package, before)
    for key, value in pairs(Framework.Config.platforms or {}) do
        if before.platforms[key] == nil then
            Framework.Owners.platforms[key] = package.id
        else
            claimLegacyEntry("platforms", key, value, before.platforms[key], package)
        end
    end
    for key, value in pairs(Framework.Config.weapons or {}) do
        if before.weapons[key] == nil then
            Framework.Owners.weapons[key] = package.id
        else
            claimLegacyEntry("weapons", key, value, before.weapons[key], package)
        end
    end
    for key, value in pairs(Framework.Config.parts or {}) do
        if before.parts[key] == nil then
            Framework.Owners.parts[key] = package.id
        else
            claimLegacyEntry("parts", key, value, before.parts[key], package)
        end
    end
    local profiles = Framework.Config.npcPresets and Framework.Config.npcPresets.profiles or {}
    for key, value in pairs(profiles) do
        if before.npcPresets[key] == nil then
            Framework.Owners.npcPresets[key] = package.id
        else
            claimLegacyEntry("npcPresets", key, value, before.npcPresets[key], package)
        end
    end
end

local function loadPackageEntry(package)
    if type(package.entry) ~= "string" or package.entry == "" then return end

    local oldCurrentPackage = Framework._currentPackage
    Framework._currentPackage = package

    local before = {
        platforms = snapshotEntries(Framework.Config.platforms),
        weapons = snapshotEntries(Framework.Config.weapons),
        parts = snapshotEntries(Framework.Config.parts),
        npcPresets = snapshotEntries(Framework.Config.npcPresets and Framework.Config.npcPresets.profiles or {})
    }

    local oldDeepLua = Deep_Lua
    local oldLegacyGunsmith = oldDeepLua and oldDeepLua.Gunsmith or nil
    local oldLegacyPath = oldDeepLua and oldDeepLua.Path or nil

    if package.legacyDeepLua then
        Deep_Lua = Deep_Lua or {}
        Deep_Lua.Gunsmith = Framework
        Deep_Lua.Path = package.modDir
    end

    local ok, err = pcall(dofile, package.modDir .. "/" .. package.entry)

    if package.legacyDeepLua then
        Deep_Lua = oldDeepLua
        if Deep_Lua then
            Deep_Lua.Gunsmith = oldLegacyGunsmith
            Deep_Lua.Path = oldLegacyPath
        end
    end

    Framework._currentPackage = oldCurrentPackage

    if not ok then
        error("[GunsmithFramework] failed to load package '" .. packageLabel(package) .. "': " .. tostring(err))
    end

    assignLegacyOwners(package, before)
end

function Framework.RegisterPackage(package)
    package = ensurePackage(package)
    rememberPackage(package)
    loadPackageEntry(package)
    if Framework.Hooks and Framework.Hooks.RefreshRegistrations then
        Framework.Hooks.RefreshRegistrations()
    end
end

dofile(basePath .. "/Core.lua")
dofile(basePath .. "/Stats.lua")
dofile(basePath .. "/Validation.lua")
dofile(basePath .. "/Persistence.lua")
dofile(basePath .. "/Inventory.lua")
dofile(basePath .. "/QuickMod.lua")
dofile(basePath .. "/UiSpec.lua")
dofile(basePath .. "/QuickUiSpec.lua")
dofile(basePath .. "/Runtime.lua")
dofile(basePath .. "/NpcPresets.lua")
dofile(basePath .. "/Debug.lua")
dofile(basePath .. "/Hooks.lua")

local function runStartupValidation()
    if Framework.Validation and Framework.Validation.Run then
        Framework.Validation.Run(nil, "Startup")
    end
end

if Timer and Timer.Wait then
    Timer.Wait(runStartupValidation, 1000)
else
    runStartupValidation()
end

if Framework.Validation and Framework.Validation.RegisterCommands then
    Framework.Validation.RegisterCommands()
end

if Framework.Debug and Framework.Debug.RegisterCommands then
    Framework.Debug.RegisterCommands()
end

if Framework.Hooks and Framework.Hooks.Register then
    Framework.Hooks.Register()
end
