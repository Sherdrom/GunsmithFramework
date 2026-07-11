GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = Gunsmith.Core
local Validation = {}
Gunsmith.Validation = Validation

local oldPartFields = { "texture", "source", "offset", "order", "itemIdentifier", "slot" }
local oldConfigFields = { "defaults", "rootAccepts", "slots", "requiredRootSlots", "hiddenRootSlots", "slotNames", "pathNames" }
local knownStatFields = {}
for _, key in ipairs(Gunsmith.Stats and Gunsmith.Stats.Keys or {}) do
    knownStatFields[key] = true
end

local frameworkLocalizationKeySuffixes = {
    "ui.title",
    "ui.quick_title",
    "ui.quick_root",
    "ui.close",
    "ui.current_slots",
    "ui.back",
    "ui.preview_placeholder",
    "ui.path_line",
    "ui.enter_mounts",
    "ui.part_list_title",
    "ui.part_detail_title",
    "ui.empty_part",
    "ui.weapon_root",
    "status.installed",
    "status.missing",
    "status.incompatible",
    "status.disabled",
    "status.available",
    "action.installed",
    "action.missing",
    "action.incompatible",
    "action.disabled",
    "action.remove",
    "action.install",
    "stat.ergonomics",
    "stat.none"
}

local function isArray(value)
    if type(value) ~= "table" then return false end
    local count = 0
    for key, _ in pairs(value) do
        if type(key) ~= "number" then return false end
        if key > count then count = key end
    end
    for index = 1, count do
        if value[index] == nil then return false end
    end
    return true
end

local function hasStringArray(value)
    if not isArray(value) or #value == 0 then return false end
    for _, entry in ipairs(value) do
        if type(entry) ~= "string" or entry == "" then return false end
    end
    return true
end

local function addSetValues(set, values)
    if type(values) ~= "table" then return end
    for _, value in ipairs(values) do
        set[value] = true
    end
end

local function hasAnyProvidedPart(parts, accepts)
    if type(parts) ~= "table" or type(accepts) ~= "table" then return false end
    for _, part in pairs(parts) do
        if type(part.provides) == "table" then
            for _, provided in ipairs(part.provides) do
                for _, accepted in ipairs(accepts) do
                    if provided == accepted then return true end
                end
            end
        end
    end
    return false
end

local function validOptionalPoint(value)
    return value == nil or (type(value) == "table" and type(value.x) == "number" and type(value.y) == "number")
end

local function visualComplete(visual)
    if type(visual) ~= "table" then return false end
    local source = visual.source
    local attachPoint = visual.attachPoint
    local relativeOffset = visual.relativeOffset
    return type(visual.texture) == "string" and visual.texture ~= "" and
        type(source) == "table" and type(source.x) == "number" and type(source.y) == "number" and
        type(source.w) == "number" and type(source.h) == "number" and
        (validOptionalPoint(attachPoint) and validOptionalPoint(relativeOffset)) and
        (attachPoint ~= nil or relativeOffset ~= nil)
end

local function validOptionalScale(value)
    return value == nil or (type(value) == "number" and value > 0)
end

local function validateSpriteTransform(errors, weaponId, fieldName, settings, allowOffset)
    if settings == nil then return end
    if type(settings) ~= "table" then
        table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' " .. fieldName .. " must be a table.")
        return
    end
    if not validOptionalScale(settings.scale) then
        table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' " .. fieldName .. ".scale must be a positive number.")
    end
    if settings.rotation ~= nil and type(settings.rotation) ~= "number" then
        table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' " .. fieldName .. ".rotation must be a number.")
    end
    if settings.padding ~= nil and (type(settings.padding) ~= "number" or settings.padding < 0) then
        table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' " .. fieldName .. ".padding must be a non-negative number.")
    end
    if allowOffset and not validOptionalPoint(settings.offset) then
        table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' " .. fieldName .. ".offset must contain numeric x/y.")
    end
end

local function hiddenRootPathFor(rootSlots)
    local hiddenPath = nil
    for path, root in pairs(rootSlots or {}) do
        if type(root) == "table" and root.hidden == true then
            if hiddenPath ~= nil then return nil, "multiple" end
            hiddenPath = path
        end
    end
    return hiddenPath, nil
end

local function validRelativePath(path)
    return type(path) == "string" and path ~= "" and
        string.sub(path, 1, 1) ~= "/" and
        string.sub(path, -1) ~= "/" and
        not string.find(path, "//", 1, true)
end

local function forEachPathSegment(path, callback)
    for segment in string.gmatch(path, "[^/]+") do
        callback(segment)
    end
end

local function itemPrefabExists(identifier)
    if not identifier or identifier == "" then return true end
    if not ItemPrefab or not ItemPrefab.GetItemPrefab then return true end
    return ItemPrefab.GetItemPrefab(identifier) ~= nil
end

local function reportPrefix(label, level)
    local prefix = "[GunsmithFramework][Validation]"
    if label and label ~= "" then
        prefix = prefix .. "[" .. tostring(label) .. "]"
    end
    if level then
        prefix = prefix .. "[" .. level .. "]"
    end
    return prefix
end

local function addLocalizationKey(keys, key)
    if type(key) == "string" and key ~= "" then
        keys[key] = true
    end
end

local function addFrameworkLocalizationKeys(keys, prefix)
    if type(prefix) ~= "string" or prefix == "" then return end

    for _, suffix in ipairs(frameworkLocalizationKeySuffixes) do
        addLocalizationKey(keys, prefix .. "." .. suffix)
    end
    for _, statName in ipairs(Gunsmith.Stats and Gunsmith.Stats.Keys or {}) do
        if statName ~= "Ergonomics" then
            addLocalizationKey(keys, prefix .. ".stattypes." .. statName)
        end
    end
end

local function addRegisteredFrameworkLocalizationKeys(keys)
    addFrameworkLocalizationKeys(keys, Gunsmith.LocalizationPrefix or "gunsmith.framework")
end

local function loadRegisteredLocalizationKeys()
    if not io or not io.open then
        return nil, "Lua io is unavailable"
    end

    local keys = {}
    local loadedFiles = 0
    local function loadFiles(modDir, files)
        if type(modDir) ~= "string" or type(files) ~= "table" then return end

        for _, relativePath in ipairs(files) do
            if type(relativePath) == "string" and relativePath ~= "" then
                local path = modDir .. "/" .. relativePath
                local file = io.open(path, "r")
                if file then
                    local content = file:read("*a") or ""
                    file:close()
                    loadedFiles = loadedFiles + 1
                    for key in string.gmatch(content, "<([%w%._%-]+)>") do
                        keys[key] = true
                    end
                end
            end
        end
    end

    loadFiles(Gunsmith.ModDir, Gunsmith.LocalizationFiles)

    for _, packageId in ipairs(Gunsmith.PackageOrder or {}) do
        local package = Gunsmith.Packages and Gunsmith.Packages[packageId] or nil
        if type(package) == "table" then
            loadFiles(package.modDir, package.localizationFiles)
        end
    end

    if loadedFiles == 0 then
        return nil, "no registered localization file could be opened"
    end
    return keys, nil
end

local function printReport(errors, warnings, label)
    for _, message in ipairs(errors) do
        print(reportPrefix(label, "ERROR") .. " " .. message)
    end
    for _, message in ipairs(warnings) do
        print(reportPrefix(label, "WARN") .. " " .. message)
    end
    print(string.format("%s %d errors, %d warnings.", reportPrefix(label), #errors, #warnings))
end

function Validation.Run(configOverride, label)
    local errors = {}
    local warnings = {}
    local config = configOverride or Gunsmith.Config

    if type(config) ~= "table" then
        table.insert(errors, "Missing Gunsmith.Config.")
        printReport(errors, warnings, label)
        return false
    end

    local platforms = config.platforms
    local weapons = config.weapons
    local parts = config.parts
    local profiles = config.npcPresets and config.npcPresets.profiles or {}
    if type(platforms) ~= "table" then table.insert(errors, "Missing Gunsmith.Config.platforms.") end
    if type(weapons) ~= "table" then table.insert(errors, "Missing Gunsmith.Config.weapons.") end
    if type(parts) ~= "table" then table.insert(errors, "Missing Gunsmith.Config.parts.") end
    if #errors > 0 then
        printReport(errors, warnings, label)
        return false
    end

    local defaultPartIds = {}
    local acceptedTypes = {}
    local providedTypes = {}
    local platformRootSlots = {}
    local validatedDefaultParents = {}
    local localizationKeys = {}
    addRegisteredFrameworkLocalizationKeys(localizationKeys)
    local owners = (configOverride and (configOverride.owners or configOverride.Owners)) or Gunsmith.Owners
    local packages = (configOverride and (configOverride.packages or configOverride.Packages)) or Gunsmith.Packages
    local enforceOwners = configOverride == nil or type(owners) == "table"

    local function ownerFor(kind, key)
        local ownerTable = type(owners) == "table" and owners[kind] or nil
        if type(ownerTable) ~= "table" or type(key) ~= "string" or key == "" then return nil end
        return ownerTable[key]
    end

    local function packageFor(ownerId)
        return type(packages) == "table" and packages[ownerId] or nil
    end

    local function ownerCanReference(ownerId, targetOwnerId)
        if not enforceOwners then return true end
        if type(ownerId) ~= "string" or ownerId == "" then return false end
        if type(targetOwnerId) ~= "string" or targetOwnerId == "" then return false end
        if ownerId == targetOwnerId then return true end
        local package = packageFor(ownerId)
        if type(package) ~= "table" then return false end
        if type(package._importSet) == "table" and package._importSet[targetOwnerId] == true then return true end
        if type(package.imports) == "table" then
            for _, importId in ipairs(package.imports) do
                if importId == targetOwnerId then return true end
            end
        end
        return false
    end

    local function validateOwned(kind, key, label)
        if not enforceOwners then return end
        local ownerId = ownerFor(kind, key)
        if type(ownerId) ~= "string" or ownerId == "" then
            table.insert(errors, label .. " is not owned by any registered package.")
        elseif type(packages) == "table" and type(packages[ownerId]) ~= "table" then
            table.insert(errors, label .. " is owned by missing package '" .. tostring(ownerId) .. "'.")
        end
    end

    local function validateReference(kind, key, ownerId, label)
        if not enforceOwners then return end
        local targetOwner = ownerFor(kind, key)
        if type(targetOwner) ~= "string" or targetOwner == "" then
            table.insert(errors, label .. " references unowned " .. kind .. " '" .. tostring(key) .. "'.")
        elseif not ownerCanReference(ownerId, targetOwner) then
            table.insert(errors, label .. " references " .. kind .. " '" .. tostring(key) .. "' owned by package '" .. tostring(targetOwner) .. "' without importing it.")
        end
    end

    local function validateImportArray(packageId, imports)
        if imports == nil then return end
        if type(imports) ~= "table" then
            table.insert(errors, "Package '" .. tostring(packageId) .. "' imports must be a string array.")
            return
        end
        local count = 0
        for key, _ in pairs(imports) do
            if type(key) ~= "number" or key < 1 or key % 1 ~= 0 then
                table.insert(errors, "Package '" .. tostring(packageId) .. "' imports must be a string array.")
                return
            end
            count = count + 1
        end
        for index = 1, count do
            local importId = imports[index]
            if type(importId) ~= "string" or importId == "" then
                table.insert(errors, "Package '" .. tostring(packageId) .. "' imports #" .. tostring(index) .. " must be a non-empty package id string.")
            elseif type(packages[importId]) ~= "table" then
                table.insert(errors, "Package '" .. tostring(packageId) .. "' imports missing package '" .. tostring(importId) .. "'.")
            end
        end
    end

    if enforceOwners then
        if type(packages) ~= "table" then
            table.insert(errors, "Missing Gunsmith.Packages for owner isolation.")
        else
            for packageId, package in pairs(packages) do
                if type(package) == "table" then
                    validateImportArray(packageId, package.imports)
                end
            end
        end
        for platformId, _ in pairs(platforms) do validateOwned("platforms", platformId, "Platform '" .. tostring(platformId) .. "'") end
        for weaponId, _ in pairs(weapons) do validateOwned("weapons", weaponId, "Weapon '" .. tostring(weaponId) .. "'") end
        for partId, _ in pairs(parts) do validateOwned("parts", partId, "Part '" .. tostring(partId) .. "'") end
        for profileName, _ in pairs(profiles) do validateOwned("npcPresets", profileName, "NPC preset '" .. tostring(profileName) .. "'") end
    end

    local function partCanAttachToMount(part, mount)
        if type(part) ~= "table" or type(mount) ~= "table" then return false end
        local expectedType = mount.partType or mount.path
        return type(expectedType) == "string" and expectedType ~= "" and
            part.type == expectedType and Core.PartProvidesAccepted(part, mount.accepts)
    end

    local function collectQuickKeys(parentPart, quickKeys, visited, depth, includeOptional, ownerId)
        if type(parentPart) ~= "table" or type(parentPart.mounts) ~= "table" or depth > 32 then return end
        for _, mount in ipairs(parentPart.mounts) do
            local quick = mount.quick
            if type(quick) == "table" and type(quick.key) == "string" and quick.key ~= "" then
                quickKeys[quick.key] = true
            end

            local childPartId = mount.defaultPart
            if type(childPartId) == "string" and childPartId ~= "" and ownerCanReference(ownerId, ownerFor("parts", childPartId)) and not visited[childPartId] then
                visited[childPartId] = true
                collectQuickKeys(parts[childPartId], quickKeys, visited, depth + 1, includeOptional, ownerId)
            end

            if includeOptional then
                for candidateId, candidatePart in pairs(parts) do
                    if ownerCanReference(ownerId, ownerFor("parts", candidateId)) and not visited[candidateId] and partCanAttachToMount(candidatePart, mount) then
                        visited[candidateId] = true
                        collectQuickKeys(candidatePart, quickKeys, visited, depth + 1, includeOptional, ownerId)
                    end
                end
            end
        end
    end

    local function reachableQuickKeysForWeapon(weapon, platformRootSlots, ownerId)
        local quickKeys = {}
        if type(weapon.roots) ~= "table" then return quickKeys end
        for path, _ in pairs(platformRootSlots or {}) do
            local root = weapon.roots[path]
            local rootPartId = type(root) == "table" and root.part or nil
            if type(rootPartId) == "string" and rootPartId ~= "" and ownerCanReference(ownerId, ownerFor("parts", rootPartId)) then
                collectQuickKeys(parts[rootPartId], quickKeys, { [rootPartId] = true }, 0, true, ownerId)
            end
        end
        return quickKeys
    end

    local function validateDefaultChildren(parentPartId, parentPart, path, stack, depth, ownerId)
        if type(parentPart.mounts) ~= "table" then return end
        if depth > 32 then
            table.insert(errors, "Default part tree under '" .. tostring(path) .. "' is deeper than 32 levels.")
            return
        end
        local validationKey = tostring(ownerId or "") .. ":" .. tostring(parentPartId)
        if validatedDefaultParents[validationKey] then return end
        validatedDefaultParents[validationKey] = true

        for _, mount in ipairs(parentPart.mounts) do
            local childPartId = mount.defaultPart
            if childPartId ~= nil then
                if type(childPartId) ~= "string" or childPartId == "" then
                    table.insert(errors, "Part '" .. tostring(parentPartId) .. "' mount '" .. tostring(mount.path) .. "' defaultPart must be a part id string.")
                else
                    local childPathSegment = mount.path
                    local childPath = path .. "/" .. tostring(childPathSegment)
                    local childPart = parts[childPartId]
                    defaultPartIds[childPartId] = true
                    validateReference("parts", childPartId, ownerId, "Part '" .. tostring(parentPartId) .. "' mount '" .. tostring(childPathSegment) .. "' defaultPart")

                    if stack[childPartId] then
                        table.insert(errors, "Mount defaultPart contains a cycle at '" .. tostring(childPartId) .. "'.")
                    elseif not childPart then
                        table.insert(errors, "Part '" .. tostring(parentPartId) .. "' mount '" .. tostring(childPathSegment) .. "' default part '" .. tostring(childPartId) .. "' does not exist.")
                    else
                        local expectedType = mount.partType or childPathSegment
                        if childPart.type ~= expectedType then
                            table.insert(errors, "Part '" .. tostring(parentPartId) .. "' mount '" .. tostring(childPathSegment) .. "' default part '" .. tostring(childPartId) .. "' type does not match '" .. tostring(expectedType) .. "'.")
                        elseif not Core.PartProvidesAccepted(childPart, mount.accepts) then
                            table.insert(errors, "Part '" .. tostring(parentPartId) .. "' mount '" .. tostring(childPathSegment) .. "' default part '" .. tostring(childPartId) .. "' is not accepted by '" .. tostring(childPath) .. "'.")
                        end

                        stack[childPartId] = true
                        validateDefaultChildren(childPartId, childPart, childPath, stack, depth + 1, ownerId)
                        stack[childPartId] = nil
                    end
                end
            end
        end
    end

    for platformId, platform in pairs(platforms) do
        if type(platform) ~= "table" then
            table.insert(errors, "Platform '" .. tostring(platformId) .. "' must be a table.")
        else
            for _, field in ipairs(oldConfigFields) do
                if platform[field] ~= nil then
                    table.insert(errors, "Platform '" .. tostring(platformId) .. "' uses removed field '" .. field .. "'.")
                end
            end
            if type(platform.rootSlots) ~= "table" then table.insert(errors, "Platform '" .. platformId .. "' is missing rootSlots.") end
            if type(platform.canvas) ~= "table" or type(platform.canvas.w) ~= "number" or type(platform.canvas.h) ~= "number" then
                table.insert(errors, "Platform '" .. platformId .. "' is missing canvas.w/h.")
            end
            if platform.visualScale ~= nil then
                table.insert(errors, "Platform '" .. platformId .. "' uses removed field 'visualScale'; use part.visual.scale.")
            end
            if platform.visualOrigin ~= nil then
                table.insert(errors, "Platform '" .. platformId .. "' uses removed field 'visualOrigin'.")
            end
            if platform.worldSpriteDepth ~= nil then
                table.insert(errors, "Platform '" .. platformId .. "' uses removed field 'worldSpriteDepth'; use the XML Sprite depth.")
            end
            if type(platform.pathNameKeys) ~= "table" then
                table.insert(errors, "Platform '" .. platformId .. "' is missing pathNameKeys.")
            else
                for path, key in pairs(platform.pathNameKeys) do
                    if type(path) ~= "string" or path == "" then
                        table.insert(errors, "Platform '" .. platformId .. "' pathNameKeys contains invalid path.")
                    elseif type(key) ~= "string" or key == "" then
                        table.insert(errors, "Platform '" .. platformId .. "' pathNameKeys['" .. path .. "'] must be a localization key string.")
                    else
                        addLocalizationKey(localizationKeys, key)
                    end
                end
            end

            local rootSlots = {}
            if type(platform.rootSlots) == "table" then
                if not isArray(platform.rootSlots) then
                    table.insert(errors, "Platform '" .. platformId .. "' rootSlots must be an array.")
                else
                    for index, root in ipairs(platform.rootSlots) do
                        local rootLabel = "Platform '" .. platformId .. "' rootSlots #" .. tostring(index)
                        if type(root) ~= "table" or type(root.path) ~= "string" or root.path == "" then
                            table.insert(errors, rootLabel .. " is missing path.")
                        elseif root.slot ~= nil then
                            table.insert(errors, rootLabel .. " uses removed field 'slot'; use 'path'.")
                        elseif rootSlots[root.path] then
                            table.insert(errors, rootLabel .. " duplicates root path '" .. root.path .. "'.")
                        else
                            rootSlots[root.path] = root
                            if type(platform.pathNameKeys) == "table" and type(platform.pathNameKeys[root.path]) ~= "string" then
                                table.insert(errors, "Platform '" .. platformId .. "' pathNameKeys missing root path '" .. root.path .. "'.")
                            end
                        end
                        if type(root) == "table" then
                            if root.required ~= nil then
                                table.insert(errors, rootLabel .. " uses removed field 'required'; root slots are always required.")
                            end
                            if root.hidden ~= nil and type(root.hidden) ~= "boolean" then
                                table.insert(errors, rootLabel .. " hidden must be boolean when declared.")
                            end
                        end
                    end
                end
            end
            if platform.requiredSlots ~= nil then
                if type(platform.requiredSlots) ~= "table" then
                    table.insert(errors, "Platform '" .. platformId .. "' requiredSlots must be a string array.")
                else
                    local hiddenPath, hiddenError = hiddenRootPathFor(rootSlots)
                    if hiddenPath == nil then
                        local reason = hiddenError == "multiple" and "multiple hidden rootSlots" or "no hidden rootSlot"
                        table.insert(errors, "Platform '" .. platformId .. "' requiredSlots uses hidden-root-relative paths but has " .. reason .. ".")
                    end
                    if not isArray(platform.requiredSlots) then
                        table.insert(errors, "Platform '" .. platformId .. "' requiredSlots must be an array, not a keyed table.")
                    else
                        local seenRequiredSlots = {}
                        for index, requiredPath in ipairs(platform.requiredSlots) do
                            local requiredLabel = "Platform '" .. platformId .. "' requiredSlots #" .. tostring(index)
                            if type(requiredPath) ~= "string" or requiredPath == "" then
                                table.insert(errors, requiredLabel .. " must be a non-empty string.")
                            elseif not validRelativePath(requiredPath) then
                                table.insert(errors, requiredLabel .. " must be a relative path without empty segments.")
                            elseif hiddenPath ~= nil and (requiredPath == hiddenPath or string.sub(requiredPath, 1, #hiddenPath + 1) == hiddenPath .. "/") then
                                table.insert(errors, requiredLabel .. " uses removed full path '" .. requiredPath .. "'; omit hidden root '" .. hiddenPath .. "'.")
                            elseif seenRequiredSlots[requiredPath] then
                                table.insert(errors, requiredLabel .. " duplicates required slot '" .. requiredPath .. "'.")
                            else
                                seenRequiredSlots[requiredPath] = true
                                if type(platform.pathNameKeys) == "table" then
                                    forEachPathSegment(requiredPath, function(segment)
                                        if type(platform.pathNameKeys[segment]) ~= "string" then
                                            table.insert(errors, "Platform '" .. platformId .. "' pathNameKeys missing required slot segment '" .. segment .. "' from '" .. requiredPath .. "'.")
                                        end
                                    end)
                                end
                            end
                        end
                    end
                end
            end
            platformRootSlots[platformId] = rootSlots
        end
    end

    for weaponId, weapon in pairs(weapons) do
        local platformId = type(weapon) == "table" and weapon.platform or nil
        local platform = type(platformId) == "string" and platforms[platformId] or nil
        local weaponOwner = ownerFor("weapons", weaponId)
        if type(weapon) ~= "table" or type(platformId) ~= "string" or not platform then
            table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' references missing platform '" .. tostring(platformId) .. "'.")
        else
            validateReference("platforms", platformId, weaponOwner, "Weapon '" .. tostring(weaponId) .. "' platform")
            if weapon.defaults ~= nil then table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' uses removed field 'defaults'.") end
            if weapon.rootAccepts ~= nil then table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' uses removed field 'rootAccepts'.") end
            if weapon.quickSlotCalibration ~= nil then table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' uses removed field 'quickSlotCalibration'.") end
            if weapon.rootParts ~= nil then table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' uses removed field 'rootParts'; use roots[].part.") end
            if weapon.rootSockets ~= nil then table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' uses removed field 'rootSockets'; use roots[].socket.") end
            if weapon.quickSlotCanvasOrigin ~= nil then table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' uses removed field 'quickSlotCanvasOrigin'.") end
            if weapon.scale ~= nil then
                table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' uses removed field 'scale'; use part.visual.scale, preview.scale, inventory.scale, or world.scale.")
            end

            local rootSlots = platformRootSlots[platformId] or {}
            if type(weapon.roots) ~= "table" then
                table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' is missing roots.")
            else
                for path, _ in pairs(rootSlots) do
                    if weapon.roots[path] == nil then
                        table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' roots missing root path '" .. tostring(path) .. "'.")
                    end
                end
                for path, root in pairs(weapon.roots) do
                    local rootLabel = "Weapon '" .. tostring(weaponId) .. "' roots '" .. tostring(path) .. "'"
                    local partId = type(root) == "table" and root.part or nil
                    local part = parts[partId]
                    if type(root) ~= "table" then
                        table.insert(errors, rootLabel .. " must be a table.")
                    elseif not rootSlots[path] then
                        table.insert(errors, rootLabel .. " contains unknown root path.")
                    elseif type(partId) ~= "string" or partId == "" then
                        table.insert(errors, rootLabel .. ".part must be a non-empty part id string.")
                    elseif not part then
                        table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' root part '" .. tostring(partId) .. "' does not exist.")
                    elseif part.type ~= path then
                        table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' root part '" .. tostring(partId) .. "' type does not match '" .. tostring(path) .. "'.")
                    else
                        validateReference("parts", partId, weaponOwner, rootLabel .. ".part")
                        defaultPartIds[partId] = true
                        validateDefaultChildren(partId, part, path, { [partId] = true }, 0, weaponOwner)
                    end
                    if type(root) == "table" then
                        if not validOptionalPoint(root.socket) or root.socket == nil then
                            table.insert(errors, rootLabel .. ".socket must contain numeric x/y.")
                        end
                    end
                end
            end

            if weapon.preview ~= nil then
                if type(weapon.preview) ~= "table" then
                    table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' preview must be a table.")
                else
                    if weapon.preview.padding ~= nil and (type(weapon.preview.padding) ~= "number" or weapon.preview.padding < 0) then
                        table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' preview.padding must be a non-negative number.")
                    end
                    if weapon.preview.zoom ~= nil then
                        table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' uses removed field 'preview.zoom'; use preview.scale.")
                    end
                    if not validOptionalScale(weapon.preview.scale) then
                        table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' preview.scale must be a positive number.")
                    end
                    if not validOptionalPoint(weapon.preview.offset) then
                        table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' preview.offset must contain numeric x/y.")
                    end
                end
            end

            validateSpriteTransform(errors, weaponId, "inventory", weapon.inventory, false)
            validateSpriteTransform(errors, weaponId, "world", weapon.world, true)

            if weapon.quickSlotBindings ~= nil then
                if type(weapon.quickSlotBindings) ~= "table" then
                    table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' quickSlotBindings must be a table.")
                else
                    local quickKeys = reachableQuickKeysForWeapon(weapon, rootSlots, weaponOwner)
                    local usedSlots = {}
                    for key, binding in pairs(weapon.quickSlotBindings) do
                        local bindingLabel = "Weapon '" .. tostring(weaponId) .. "' quickSlotBindings '" .. tostring(key) .. "'"
                        if type(key) ~= "string" or key == "" then
                            table.insert(errors, "Weapon '" .. tostring(weaponId) .. "' quickSlotBindings key must be a non-empty string.")
                        elseif not quickKeys[key] then
                            table.insert(errors, bindingLabel .. " does not match a quick mount reachable from this weapon.")
                        end

                        if type(binding) ~= "table" then
                            table.insert(errors, bindingLabel .. " must be a table.")
                        else
                            if binding.itemPos ~= nil then
                                table.insert(errors, bindingLabel .. ".itemPos is removed; use mount anchor plus optional itemPosOffset.")
                            end
                            if binding.hide ~= nil then
                                table.insert(errors, bindingLabel .. ".hide is removed; omit it.")
                            end
                            if binding.itemPosOffset ~= nil and not validOptionalPoint(binding.itemPosOffset) then
                                table.insert(errors, bindingLabel .. ".itemPosOffset must contain numeric x/y when declared.")
                            end

                            local slotIndex = binding.slot
                            if type(slotIndex) ~= "number" or slotIndex < 0 or slotIndex % 1 ~= 0 then
                                table.insert(errors, bindingLabel .. ".slot must be a non-negative integer.")
                            elseif usedSlots[slotIndex] then
                                table.insert(errors, bindingLabel .. ".slot duplicates slot " .. tostring(slotIndex) .. ".")
                            else
                                usedSlots[slotIndex] = true
                            end
                        end
                    end
                end
            end
        end
    end

    for partId, part in pairs(parts) do
        if type(part) ~= "table" then
            table.insert(errors, "Part '" .. tostring(partId) .. "' must be a table.")
        else
            if type(part.type) ~= "string" or part.type == "" then table.insert(errors, "Part '" .. partId .. "' is missing type.") end
            if part.name ~= nil then
                table.insert(errors, "Part '" .. partId .. "' uses removed field 'name'; use nameKey.")
            end
            if type(part.nameKey) ~= "string" or part.nameKey == "" then
                table.insert(errors, "Part '" .. partId .. "' is missing nameKey.")
            else
                addLocalizationKey(localizationKeys, part.nameKey)
            end
            if not hasStringArray(part.provides) then
                table.insert(errors, "Part '" .. partId .. "' is missing provides.")
            else
                addSetValues(providedTypes, part.provides)
            end
            if part.excludes ~= nil then
                if not hasStringArray(part.excludes) then
                    table.insert(errors, "Part '" .. partId .. "' excludes must be a non-empty string array when declared.")
                else
                    local seenExcludes = {}
                    local partOwner = ownerFor("parts", partId)
                    for _, excludedPartId in ipairs(part.excludes) do
                        if excludedPartId == partId then
                            table.insert(errors, "Part '" .. partId .. "' excludes itself.")
                        elseif not parts[excludedPartId] then
                            table.insert(errors, "Part '" .. partId .. "' excludes missing part '" .. tostring(excludedPartId) .. "'.")
                        elseif seenExcludes[excludedPartId] then
                            table.insert(errors, "Part '" .. partId .. "' excludes duplicate part '" .. tostring(excludedPartId) .. "'.")
                        else
                            validateReference("parts", excludedPartId, partOwner, "Part '" .. partId .. "' excludes")
                        end
                        seenExcludes[excludedPartId] = true
                    end
                end
            end

            for _, field in ipairs(oldPartFields) do
                if part[field] ~= nil then
                    table.insert(errors, "Part '" .. partId .. "' uses removed field '" .. field .. "'.")
                end
            end
            if part.quickItemPosOffset ~= nil then
                table.insert(errors, "Part '" .. partId .. "' uses removed field 'quickItemPosOffset'.")
            end

            if part.visual ~= nil and not visualComplete(part.visual) then
                table.insert(errors, "Part '" .. partId .. "' has incomplete visual data.")
            end
            if part.visual ~= nil and part.visual.offset ~= nil then
                table.insert(errors, "Part '" .. partId .. "' visual.offset is removed; use visual.attachPoint.")
            end
            if part.visual ~= nil and not validOptionalScale(part.visual.scale) then
                table.insert(errors, "Part '" .. partId .. "' visual.scale must be a positive number.")
            end
            if part.stats ~= nil then
                if type(part.stats) ~= "table" then
                    table.insert(errors, "Part '" .. partId .. "' stats must be a table.")
                else
                    for statName, statValue in pairs(part.stats) do
                        if not knownStatFields[statName] then
                            table.insert(warnings, "Part '" .. partId .. "' uses unknown stat '" .. tostring(statName) .. "'.")
                        elseif type(statValue) ~= "number" then
                            table.insert(errors, "Part '" .. partId .. "' stat '" .. statName .. "' must be a number.")
                        end
                    end
                end
            end

            local item = part.item
            if not defaultPartIds[partId] then
                if type(item) ~= "table" or (type(item.identifier) ~= "string" and item.virtual ~= true) then
                    table.insert(errors, "Non-default part '" .. partId .. "' must declare item.identifier or item.virtual = true.")
                end
            end

            if type(item) == "table" and type(item.identifier) == "string" and not itemPrefabExists(item.identifier) then
                table.insert(warnings, "Part '" .. partId .. "' item prefab not found: " .. item.identifier)
            end

            if part.mounts ~= nil then
                if not isArray(part.mounts) then
                    table.insert(errors, "Part '" .. partId .. "' mounts must be an array.")
                else
                    for index, mount in ipairs(part.mounts) do
                        local mountLabel = "Part '" .. partId .. "' mount #" .. tostring(index)
                        if mount.slot ~= nil then
                            table.insert(errors, mountLabel .. " uses removed field 'slot'; use 'path'.")
                        end
                        if type(mount.path) ~= "string" or mount.path == "" then
                            table.insert(errors, mountLabel .. " is missing path.")
                        end
                        if mount.partSlot ~= nil then
                            table.insert(errors, mountLabel .. " uses removed field 'partSlot'; use 'partType'.")
                        end
                        if mount.partType ~= nil and (type(mount.partType) ~= "string" or mount.partType == "") then
                            table.insert(errors, mountLabel .. " partType must be a non-empty string when declared.")
                        end
                        if mount.name ~= nil then
                            table.insert(errors, mountLabel .. " uses removed field 'name'; use nameKey.")
                        end
                        if mount.nameKey ~= nil then
                            if type(mount.nameKey) ~= "string" or mount.nameKey == "" then
                                table.insert(errors, mountLabel .. " nameKey must be a non-empty string when declared.")
                            else
                                addLocalizationKey(localizationKeys, mount.nameKey)
                            end
                        end
                        if mount.defaultPart ~= nil and (type(mount.defaultPart) ~= "string" or mount.defaultPart == "") then
                            table.insert(errors, mountLabel .. " defaultPart must be a non-empty string when declared.")
                        end
                        if not hasStringArray(mount.accepts) then
                            table.insert(errors, mountLabel .. " is missing accepts.")
                        else
                            addSetValues(acceptedTypes, mount.accepts)
                            if not hasAnyProvidedPart(parts, mount.accepts) then
                                table.insert(warnings, mountLabel .. " accepts no currently provided part type.")
                            end
                        end
                        if not validOptionalPoint(mount.anchor) then
                            table.insert(errors, mountLabel .. " anchor must contain numeric x/y.")
                        end
                        if mount.visualOrder ~= nil and type(mount.visualOrder) ~= "number" then
                            table.insert(errors, mountLabel .. " visualOrder must be a number when declared.")
                        end
                        if mount.quick ~= nil then
                            if type(mount.quick) ~= "table" then
                                table.insert(errors, mountLabel .. " quick must be a table when declared.")
                            else
                                if type(mount.quick.key) ~= "string" or mount.quick.key == "" then
                                    table.insert(errors, mountLabel .. " quick.key must be a non-empty string.")
                                end
                                if mount.quick.nameKey ~= nil then
                                    if type(mount.quick.nameKey) ~= "string" or mount.quick.nameKey == "" then
                                        table.insert(errors, mountLabel .. " quick.nameKey must be a non-empty string when declared.")
                                    else
                                        addLocalizationKey(localizationKeys, mount.quick.nameKey)
                                    end
                                end
                                if mount.quick.showWhenContained ~= nil and not hasStringArray(mount.quick.showWhenContained) then
                                    table.insert(errors, mountLabel .. " quick.showWhenContained must be a non-empty string array when declared.")
                                end
                            end
                        end
                    end
                end
            end
            if part.defaultParts ~= nil then
                table.insert(errors, "Part '" .. partId .. "' uses removed field 'defaultParts'; use mounts[].defaultPart.")
            end
        end
    end

    for partId, part in pairs(parts) do
        if type(part) == "table" and type(part.mounts) == "table" then
            validateDefaultChildren(partId, part, part.type or partId, { [partId] = true }, 0, ownerFor("parts", partId))
        end
    end

    for profileName, profile in pairs(profiles) do
        local profileOwner = ownerFor("npcPresets", profileName)
        if type(profile) ~= "table" then
            table.insert(errors, "NPC preset '" .. tostring(profileName) .. "' must be a table.")
        else
            if profile.weapon ~= nil then
                if type(profile.weapon) ~= "string" or profile.weapon == "" then
                    table.insert(errors, "NPC preset '" .. tostring(profileName) .. "' weapon must be a non-empty string when declared.")
                elseif type(weapons[profile.weapon]) ~= "table" then
                    table.insert(warnings, "NPC preset '" .. tostring(profileName) .. "' references missing weapon '" .. tostring(profile.weapon) .. "'.")
                else
                    validateReference("weapons", profile.weapon, profileOwner, "NPC preset '" .. tostring(profileName) .. "' weapon")
                    local weaponOwner = ownerFor("weapons", profile.weapon)
                    if enforceOwners and not ownerCanReference(weaponOwner, profileOwner) then
                        table.insert(errors, "Weapon '" .. tostring(profile.weapon) .. "' cannot use NPC preset '" .. tostring(profileName) .. "' owned by package '" .. tostring(profileOwner or "<none>") .. "' without importing it.")
                    end
                end
            end
            if profile.parts ~= nil then
                if type(profile.parts) ~= "table" then
                    table.insert(errors, "NPC preset '" .. tostring(profileName) .. "' parts must be a table when declared.")
                else
                    for path, partId in pairs(profile.parts) do
                        if type(path) ~= "string" or path == "" then
                            table.insert(errors, "NPC preset '" .. tostring(profileName) .. "' parts contains an invalid path.")
                        elseif type(partId) ~= "string" or partId == "" then
                            table.insert(errors, "NPC preset '" .. tostring(profileName) .. "' part at path '" .. tostring(path) .. "' must be a non-empty part id string.")
                        elseif not parts[partId] then
                            table.insert(warnings, "NPC preset '" .. tostring(profileName) .. "' references missing part '" .. tostring(partId) .. "'.")
                        else
                            validateReference("parts", partId, profileOwner, "NPC preset '" .. tostring(profileName) .. "' part '" .. tostring(path) .. "'")
                        end
                    end
                end
            end
        end
    end

    for provided, _ in pairs(providedTypes) do
        if not acceptedTypes[provided] then
            local acceptedByRoot = false
            for _, weapon in pairs(weapons) do
                if type(weapon) == "table" and type(weapon.roots) == "table" then
                    for _, root in pairs(weapon.roots) do
                        local rootPartId = type(root) == "table" and root.part or nil
                        local rootPart = parts[rootPartId]
                        if rootPart and Core.PartProvidesAccepted(rootPart, { provided }) then
                            acceptedByRoot = true
                            break
                        end
                    end
                end
                if acceptedByRoot then break end
            end
            if not acceptedByRoot then
                table.insert(warnings, "Provided type '" .. provided .. "' is not accepted by any mount.")
            end
        end
    end

    if configOverride == nil then
        local registeredKeys, localizationLoadError = loadRegisteredLocalizationKeys()
        if registeredKeys then
            for key, _ in pairs(localizationKeys) do
                if not registeredKeys[key] then
                    table.insert(errors, "Missing registered localization key '" .. key .. "'.")
                end
            end
        else
            table.insert(warnings, "Could not verify registered localization keys: " .. tostring(localizationLoadError) .. ".")
        end
    end

    printReport(errors, warnings, label)
    return #errors == 0
end

local function runPersistenceJsonSelfTest()
    local Persistence = Gunsmith.Persistence
    assert(type(Persistence) == "table")

    local platform = {
        rootSlots = {
            { path = "receiver" },
            { path = "stock" }
        }
    }
    local selection = {
        receiver = "receiver \"quote\" \\ slash\nline\ttab 世界",
        ["receiver/barrel"] = "barrel \"quote\" \\ slash\nline\ttab 世界"
    }
    local encoded = Persistence.Encode(selection, platform, {})
    local decoded = Persistence.Decode(encoded)
    local legacy = Persistence.Decode("{\"v\":1,\"parts\":{\"receiver\":\"legacy-receiver\",\"receiver/barrel\":\"legacy-barrel\"}}")

    assert(encoded == Persistence.Encode(selection, platform, {}))
    assert(type(decoded) == "table")
    assert(decoded.receiver == selection.receiver)
    assert(decoded.stock == Gunsmith.EmptyPartId)
    assert(decoded["receiver/barrel"] == selection["receiver/barrel"])
    assert(legacy.receiver == "legacy-receiver")
    assert(legacy["receiver/barrel"] == "legacy-barrel")
    assert(Persistence.Decode("{\"v\":1,\"parts\":") == nil)
    assert(Persistence.Decode("{\"v\":2,\"parts\":{}}") == nil)
end

function Validation.RunSelfTest()
    local badConfig = {
        platforms = {
            broken = {
                rootSlots = {
                    { path = "receiver", required = true, hidden = true },
                    { path = "receiver", hidden = "bad" }
                },
                defaults = {
                    receiver = "old_default"
                },
                requiredSlots = {
                    "receiver/barrel",
                    42
                },
                canvas = { w = 512, h = 160 },
                visualScale = -1,
                visualOrigin = { x = "bad", y = 80 },
                worldSpriteDepth = "bad",
                pathNames = {
                    receiver = "Old Name"
                },
                pathNameKeys = {
                    receiver = ""
                }
            },
            nested_broken = {
                rootSlots = {
                    { path = "receiver", hidden = true }
                },
                canvas = { w = 512, h = 160 }
            }
        },
        weapons = {
            bad_platform = {
                platform = "missing_platform"
            },
            bad_scale = {
                platform = "broken",
                scale = 0,
                preview = {
                    zoom = 1.0,
                    scale = 0
                },
                rootParts = {
                    receiver = "missing_root_part"
                },
                rootSockets = {
                    receiver = { x = 0, y = 0 }
                },
                world = {
                    scale = 0,
                    rotation = "bad",
                    padding = -1,
                    offset = { x = "bad", y = 0 }
                }
            },
            missing_root_part = {
                platform = "nested_broken",
                rootParts = {},
                rootSockets = {
                    receiver = { x = 0, y = 0 }
                }
            },
            bad_root_socket = {
                platform = "nested_broken",
                rootParts = {
                    receiver = "test_receiver_part",
                    magazine = "test_receiver_part"
                },
                rootSockets = {
                    receiver = { x = "bad", y = 0 },
                    magazine = { x = 0, y = 0 }
                },
                defaults = {
                    receiver = "old_weapon_default"
                },
                rootAccepts = {
                    receiver = { "old_accepts" }
                }
            }
        },
        parts = {
            test_receiver_part = {
                type = "receiver",
                nameKey = "gunsmith.framework.test.part.receiver",
                provides = { "test_receiver" },
                item = { virtual = true },
                visual = {
                    texture = "test_receiver",
                    source = { x = 0, y = 0, w = 16, h = 16 },
                    attachPoint = { x = 0, y = 0 }
                },
                mounts = {
                    { path = "barrel", accepts = { "test_barrel" }, defaultPart = "bad_type_part", anchor = { x = 0, y = 0 } },
                    { path = "stock", accepts = { "test_stock" }, defaultPart = "cycle_part", anchor = { x = 0, y = 0 } },
                    { path = "bad_default", accepts = { "test_stock" }, defaultPart = 42, anchor = { x = 0, y = 0 } }
                }
            },
            bad_type_part = {
                type = "stock",
                nameKey = "gunsmith.framework.test.part.bad_type",
                provides = { "test_barrel" },
                excludes = { 42 },
                item = { virtual = true },
                visual = {
                    texture = "bad_type",
                    source = { x = 0, y = 0, w = 16, h = 16 },
                    attachPoint = { x = 0, y = 0 }
                }
            },
            test_stock_part = {
                type = "stock",
                nameKey = "gunsmith.framework.test.part.stock",
                provides = { "test_stock" },
                excludes = { "missing_excluded_part" },
                item = { virtual = true },
                visual = {
                    texture = "test_stock",
                    source = { x = 0, y = 0, w = 16, h = 16 },
                    attachPoint = { x = 0, y = 0 }
                }
            },
            cycle_part = {
                type = "stock",
                nameKey = "gunsmith.framework.test.part.cycle",
                provides = { "test_stock" },
                excludes = { "cycle_part" },
                item = { virtual = true },
                mounts = {
                    { path = "stock", accepts = { "test_stock" }, defaultPart = "cycle_part", anchor = { x = 0, y = 0 } }
                }
            },
            old_field_part = {
                type = "receiver",
                name = "Old Field Part",
                provides = { "test_receiver" },
                excludes = "bad_excludes",
                texture = "removed_field"
            },
            bad_visual_part = {
                type = "receiver",
                nameKey = "gunsmith.framework.test.part.bad_visual",
                provides = { "test_receiver" },
                item = { virtual = true },
                stats = {
                    Ergonomics = "heavy",
                    mysteryStat = 1
                },
                visual = {
                    texture = "missing_offset",
                    source = { x = 0, y = 0, w = 16, h = 16 }
                }
            },
            bad_mount_part = {
                type = "receiver",
                nameKey = "gunsmith.framework.test.part.bad_mount",
                provides = { "unused_test_type" },
                item = { virtual = true },
                mounts = {
                    { slot = "old_path_field", path = "optic_mount", name = "Old Mount Name", nameKey = "", partSlot = "", partType = "", anchor = { x = "bad", y = 0 }, visualOrder = "bad" }
                }
            }
        }
    }

    local ownerIsolationBadConfig = {
        packages = {
            A = { id = "A", imports = {} },
            B = { id = "B", imports = {} }
        },
        owners = {
            platforms = { b_platform = "B" },
            weapons = { a_weapon = "A" },
            parts = { b_receiver = "B" },
            npcPresets = {}
        },
        platforms = {
            b_platform = {
                canvas = { w = 128, h = 64 },
                rootSlots = { { path = "receiver" } },
                pathNameKeys = { receiver = "gunsmith.framework.test.path.receiver" }
            }
        },
        weapons = {
            a_weapon = {
                platform = "b_platform",
                roots = {
                    receiver = {
                        part = "b_receiver",
                        socket = { x = 0, y = 0 }
                    }
                }
            }
        },
        parts = {
            b_receiver = {
                type = "receiver",
                nameKey = "gunsmith.framework.test.part.receiver",
                provides = { "receiver" },
                item = { virtual = true }
            }
        }
    }

    local ownerIsolationAllowedConfig = {
        packages = {
            A = { id = "A", imports = { "B" } },
            B = { id = "B", imports = {} }
        },
        owners = {
            platforms = { b_platform = "B" },
            weapons = { a_weapon = "A" },
            parts = { b_receiver = "B" },
            npcPresets = {}
        },
        platforms = ownerIsolationBadConfig.platforms,
        weapons = ownerIsolationBadConfig.weapons,
        parts = ownerIsolationBadConfig.parts
    }

    print("[GunsmithFramework][Validation][SelfTest] Running persistence JSON round-trip test.")
    runPersistenceJsonSelfTest()
    print("[GunsmithFramework][Validation][SelfTest] Running intentional broken-config validation test.")
    local result = Validation.Run(badConfig, "SelfTest")
    print("[GunsmithFramework][Validation][SelfTest] Running owner-isolation rejection test.")
    Validation.Run(ownerIsolationBadConfig, "OwnerIsolationSelfTest")
    print("[GunsmithFramework][Validation][SelfTest] Running owner-isolation import allow test.")
    Validation.Run(ownerIsolationAllowedConfig, "OwnerIsolationAllowedSelfTest")
    return result
end

function Validation.RegisterCommands()
    if Validation.CommandsRegistered or not Game or not Game.AddCommand then return end
    Validation.CommandsRegistered = true

    Game.AddCommand("GunsmithFrameworkValidate", "Run GunsmithFramework config validation", function()
        Validation.Run()
    end, nil, false)

    Game.AddCommand("GunsmithFrameworkValidationSelfTest", "Run intentional GunsmithFramework validation errors without changing real config", function()
        Validation.RunSelfTest()
    end, nil, false)
end
