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

local function normalizePath(path)
    if type(path) ~= "string" or path == "" then return nil end
    return (string.gsub(path, "\\", "/"))
end

local function trimTrailingSlash(path)
    path = normalizePath(path)
    if not path then return nil end
    return (string.gsub(path, "/+$", ""))
end

local function basename(path)
    path = trimTrailingSlash(path)
    if not path then return nil end
    return string.match(path, "([^/]+)$")
end

local function sanitizeId(value)
    if type(value) ~= "string" or value == "" then return nil end
    local id = string.lower(value)
    id = string.gsub(id, "[^%w]+", "_")
    id = string.gsub(id, "^_+", "")
    id = string.gsub(id, "_+$", "")
    if id == "" then return nil end
    return id
end

local function inferModDirFromSource(source)
    source = normalizePath(source)
    if not source then return nil end
    if string.sub(source, 1, 1) == "@" then
        source = string.sub(source, 2)
    end

    for _, marker in ipairs({ "/Lua/Autorun/", "/Lua/Scripts/" }) do
        local markerIndex = string.find(source, marker, 1, true)
        if markerIndex then
            return string.sub(source, 1, markerIndex - 1)
        end
    end

    return string.match(source, "^(.*)/[^/]*$")
end

local function inferCallerModDir(stackLevel)
    if not debug or not debug.getinfo then return nil end
    local info = debug.getinfo(stackLevel or 3, "S")
    return inferModDirFromSource(info and info.source or nil)
end

local function addUnique(values, value)
    if type(value) ~= "string" or value == "" then return end
    for _, existing in ipairs(values) do
        if existing == value then return end
    end
    table.insert(values, value)
end

local function relativeModPath(path)
    path = normalizePath(path)
    if not path then return nil end
    path = string.gsub(path, "^%%ModDir%%/", "")
    path = string.gsub(path, "^%./", "")
    if string.sub(path, 1, 1) == "/" then return nil end
    return path
end

local function fileExists(path)
    if not io or not io.open then return false end
    local file = io.open(path, "r")
    if not file then return false end
    file:close()
    return true
end

local function readFile(path)
    if not io or not io.open then return nil end
    local file = io.open(path, "r")
    if not file then return nil end
    local content = file:read("*a") or ""
    file:close()
    return content
end

local function filelistContent(modDir)
    modDir = trimTrailingSlash(modDir)
    if not modDir then return nil end
    return readFile(modDir .. "/filelist.xml")
end

local function discoverPackageName(modDir)
    local content = filelistContent(modDir)
    if not content then return nil end
    return string.match(content, "<%s*contentpackage%s+[^>]-name%s*=%s*\"([^\"]+)\"")
        or string.match(content, "<%s*contentpackage%s+[^>]-name%s*=%s*'([^']+)'")
end

local function discoverFilelistFiles(modDir, elementName)
    local files = {}
    local content = filelistContent(modDir)
    if not content then return files end

    local pattern = "<%s*" .. elementName .. "%s+[^>]-file%s*=%s*\"([^\"]+)\""
    for filePath in string.gmatch(content, pattern) do
        addUnique(files, relativeModPath(filePath))
    end
    pattern = "<%s*" .. elementName .. "%s+[^>]-file%s*=%s*'([^']+)'"
    for filePath in string.gmatch(content, pattern) do
        addUnique(files, relativeModPath(filePath))
    end

    return files
end

local function discoverLocalizationFiles(modDir)
    local files = {}
    modDir = trimTrailingSlash(modDir)
    if not modDir or not io or not io.open then return files end

    files = discoverFilelistFiles(modDir, "Text")

    if #files == 0 then
        for _, candidate in ipairs({ "text/English.xml", "text/Chinese.xml" }) do
            if fileExists(modDir .. "/" .. candidate) then
                addUnique(files, candidate)
            end
        end
    end

    return files
end

local function entryUsesLegacyDeepLua(package)
    if type(package) ~= "table" or type(package.modDir) ~= "string" or type(package.entry) ~= "string" then
        return false
    end
    local content = readFile(package.modDir .. "/" .. package.entry)
    if not content then return false end
    return string.find(content, "Deep_Lua%.Gunsmith") ~= nil or string.find(content, "Deep_Lua%.Path") ~= nil
end

local function ensurePackage(package, callerModDir)
    if type(package) == "string" then
        package = { entry = package }
    elseif type(package) ~= "table" then
        error("[GunsmithFramework] package must be a table or entry path string.")
    end
    if package._normalized then return package end

    if type(package.entry) ~= "string" or package.entry == "" then
        error("[GunsmithFramework] package.entry is required.")
    end

    package.modDir = trimTrailingSlash(package.modDir or package.modPath or callerModDir)
    if type(package.modDir) ~= "string" or package.modDir == "" then
        error("[GunsmithFramework] package.modDir could not be inferred for entry '" .. tostring(package.entry) .. "'. Pass modDir explicitly if this Lua environment does not expose caller source paths.")
    end

    package.id = package.id or sanitizeId(basename(package.modDir) or package.name or package.entry)
    if type(package.id) ~= "string" or package.id == "" then
        error("[GunsmithFramework] package.id is required.")
    end

    package._autoWeaponTags = package.weaponTags == nil
    package._autoPartTags = package.partTags == nil
    package._autoLocalizationPrefix = package.localizationPrefix == nil
    package.weaponTags = package.weaponTags or {}
    package.partTags = package.partTags or {}
    package.localizationFiles = package.localizationFiles or discoverLocalizationFiles(package.modDir)
    package.localizationPrefix = package.localizationPrefix or (package.id .. ".gunsmith")
    package.name = package.name or discoverPackageName(package.modDir) or basename(package.modDir) or package.id
    if package.legacyDeepLua == nil then
        package.legacyDeepLua = entryUsesLegacyDeepLua(package)
    end
    package._normalized = true
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

local function addLocalizationPrefixCount(counts, key)
    if type(key) ~= "string" or key == "" then return end
    for _, marker in ipairs({ ".path.", ".part.", ".mount.", ".ui.", ".status.", ".action.", ".stat.", ".stattypes." }) do
        local index = string.find(key, marker, 1, true)
        if index and index > 1 then
            local prefix = string.sub(key, 1, index - 1)
            counts[prefix] = (counts[prefix] or 0) + 1
            return
        end
    end
end

local function inferLocalizationPrefix(package)
    local counts = {}

    for platformId, platform in pairs(Framework.Config.platforms or {}) do
        if Framework.Owners.platforms[platformId] == package.id and type(platform) == "table" then
            addLocalizationPrefixCount(counts, platform.localizationPrefix)
            if type(platform.pathNameKeys) == "table" then
                for _, key in pairs(platform.pathNameKeys) do
                    addLocalizationPrefixCount(counts, key)
                end
            end
        end
    end

    for partId, part in pairs(Framework.Config.parts or {}) do
        if Framework.Owners.parts[partId] == package.id and type(part) == "table" then
            addLocalizationPrefixCount(counts, part.nameKey)
            if type(part.mounts) == "table" then
                for _, mount in ipairs(part.mounts) do
                    if type(mount) == "table" then
                        addLocalizationPrefixCount(counts, mount.nameKey)
                    end
                end
            end
        end
    end

    local bestPrefix = nil
    local bestCount = 0
    for prefix, count in pairs(counts) do
        if count > bestCount or (count == bestCount and (not bestPrefix or prefix < bestPrefix)) then
            bestPrefix = prefix
            bestCount = count
        end
    end
    return bestPrefix
end

local function attributeValue(attributes, name)
    if type(attributes) ~= "string" or type(name) ~= "string" then return nil end
    name = string.lower(name)
    for key, value in string.gmatch(attributes, "([%w_%-]+)%s*=%s*\"([^\"]*)\"") do
        if string.lower(key) == name then return value end
    end
    for key, value in string.gmatch(attributes, "([%w_%-]+)%s*=%s*'([^']*)'") do
        if string.lower(key) == name then return value end
    end
    return nil
end

local function splitTags(value)
    local tags = {}
    if type(value) ~= "string" then return tags end
    for rawTag in string.gmatch(value, "[^,]+") do
        local tag = string.gsub(rawTag, "^%s+", "")
        tag = string.gsub(tag, "%s+$", "")
        if tag ~= "" then
            table.insert(tags, tag)
        end
    end
    return tags
end

local function packageItemTags(package)
    if package._itemTagsByIdentifier then return package._itemTagsByIdentifier end

    local tagsByIdentifier = {}
    for _, relativePath in ipairs(discoverFilelistFiles(package.modDir, "Item")) do
        local content = readFile(package.modDir .. "/" .. relativePath)
        if content then
            for attributes in string.gmatch(content, "<%s*Item%s+([^>]*)>") do
                local identifier = attributeValue(attributes, "identifier")
                local tagSpec = attributeValue(attributes, "tags")
                if type(identifier) == "string" and identifier ~= "" and type(tagSpec) == "string" then
                    tagsByIdentifier[identifier] = splitTags(tagSpec)
                end
            end
        end
    end

    package._itemTagsByIdentifier = tagsByIdentifier
    return tagsByIdentifier
end

local commonItemTags = {
    smallitem = true,
    mediumitem = true,
    largeitem = true,
    weapon = true,
    gun = true,
    mountableweapon = true,
    mobilecontainer = true,
    container = true,
    medical = true,
    tool = true,
    part = true,
    accessory = true
}

local function dominantIdentifierPrefix(package)
    local counts = {}
    local function addIdentifier(identifier)
        if type(identifier) ~= "string" then return end
        local prefix = string.match(identifier, "^([%w]+)_")
        if prefix and #prefix >= 3 then
            counts[prefix] = (counts[prefix] or 0) + 1
        end
    end

    for weaponId, _ in pairs(Framework.Config.weapons or {}) do
        if Framework.Owners.weapons[weaponId] == package.id then
            addIdentifier(weaponId)
        end
    end
    for partId, part in pairs(Framework.Config.parts or {}) do
        if Framework.Owners.parts[partId] == package.id then
            addIdentifier(partId)
            if type(part) == "table" and type(part.item) == "table" then
                addIdentifier(part.item.identifier)
            end
        end
    end

    local bestPrefix = nil
    local bestCount = 0
    for prefix, count in pairs(counts) do
        if count > bestCount or (count == bestCount and (not bestPrefix or prefix < bestPrefix)) then
            bestPrefix = prefix
            bestCount = count
        end
    end
    return bestPrefix
end

local function sortedSetValues(set)
    local values = {}
    for value, _ in pairs(set) do
        table.insert(values, value)
    end
    table.sort(values)
    return values
end

local function inferPackageTags(package)
    local tagsByIdentifier = packageItemTags(package)
    local customPrefix = dominantIdentifierPrefix(package)
    local weaponTags = {}
    local partTags = {}

    for weaponId, _ in pairs(Framework.Config.weapons or {}) do
        if Framework.Owners.weapons[weaponId] == package.id then
            for _, tag in ipairs(tagsByIdentifier[weaponId] or {}) do
                local normalized = string.lower(tag)
                if normalized ~= "gunsmith" and string.find(normalized, "gunsmith", 1, true) then
                    weaponTags[tag] = true
                end
            end
        end
    end

    for partId, part in pairs(Framework.Config.parts or {}) do
        if type(part) == "table" and type(part.item) == "table" and type(part.item.identifier) == "string" then
            if Framework.Owners.parts[partId] == package.id then
                for _, tag in ipairs(tagsByIdentifier[part.item.identifier] or {}) do
                    local normalized = string.lower(tag)
                    if not commonItemTags[normalized] and (
                        string.find(normalized, "gunsmith", 1, true) or
                        string.find(normalized, "muzzle", 1, true) or
                        string.find(normalized, "sub_hanging", 1, true) or
                        string.find(normalized, "accessory", 1, true) or
                        (customPrefix and string.sub(normalized, 1, #customPrefix + 1) == customPrefix .. "_")
                    ) then
                        partTags[tag] = true
                    end
                end
            end
        end
    end

    return sortedSetValues(weaponTags), sortedSetValues(partTags)
end

local function inferPackageMetadata(package)
    if package._autoLocalizationPrefix then
        package.localizationPrefix = inferLocalizationPrefix(package) or package.localizationPrefix
    end

    local weaponTags, partTags = inferPackageTags(package)
    if package._autoWeaponTags and #weaponTags > 0 then
        package.weaponTags = weaponTags
    end
    if package._autoPartTags and #partTags > 0 then
        package.partTags = partTags
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
    inferPackageMetadata(package)
end

function Framework.RegisterPackage(package)
    package = ensurePackage(package, inferCallerModDir(3))
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
