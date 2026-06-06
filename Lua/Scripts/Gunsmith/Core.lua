GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = {}
Gunsmith.Core = Core

function Core.ItemIdentifier(item)
    if not item or not item.Prefab then return nil end
    return item.Prefab.Identifier.Value
end

function Core.ItemKey(item)
    if not item then return nil end
    return tostring(item.ID)
end

function Core.WeaponConfig(item)
    local config = Gunsmith.Config
    local identifier = Core.ItemIdentifier(item)
    if not config or not identifier then return nil end
    return config.weapons[identifier]
end

function Core.PlatformConfig(item)
    local config = Gunsmith.Config
    local weapon = Core.WeaponConfig(item)
    if not config or not weapon then return nil end
    return config.platforms[weapon.platform]
end

function Core.PackageForWeaponId(identifier)
    local ownerId = Gunsmith.Owners and Gunsmith.Owners.weapons and Gunsmith.Owners.weapons[identifier]
    if ownerId and Gunsmith.Packages then
        return Gunsmith.Packages[ownerId]
    end
    return nil
end

function Core.PackageForPlatform(platform)
    if type(platform) ~= "table" or not Gunsmith.Config or not Gunsmith.Config.platforms then return nil end
    for platformId, candidate in pairs(Gunsmith.Config.platforms) do
        if candidate == platform then
            local ownerId = Gunsmith.Owners and Gunsmith.Owners.platforms and Gunsmith.Owners.platforms[platformId]
            return ownerId and Gunsmith.Packages and Gunsmith.Packages[ownerId] or nil
        end
    end
    return nil
end

function Core.LocalizationPrefixForWeaponId(identifier)
    local config = Gunsmith.Config
    local weapon = config and config.weapons and config.weapons[identifier] or nil
    if type(weapon) == "table" and type(weapon.localizationPrefix) == "string" and weapon.localizationPrefix ~= "" then
        return weapon.localizationPrefix
    end

    local package = Core.PackageForWeaponId(identifier)
    if package and type(package.localizationPrefix) == "string" and package.localizationPrefix ~= "" then
        return package.localizationPrefix
    end
    return Gunsmith.LocalizationPrefix or "gunsmith.framework"
end

function Core.LocalizationPrefixForItem(item)
    return Core.LocalizationPrefixForWeaponId(Core.ItemIdentifier(item))
end

function Core.LocalizationPrefixForPlatform(platform)
    if type(platform) == "table" and type(platform.localizationPrefix) == "string" and platform.localizationPrefix ~= "" then
        return platform.localizationPrefix
    end

    local package = Core.PackageForPlatform(platform)
    if package and type(package.localizationPrefix) == "string" and package.localizationPrefix ~= "" then
        return package.localizationPrefix
    end
    return Gunsmith.LocalizationPrefix or "gunsmith.framework"
end

function Core.LocalizationKey(prefix, suffix)
    return tostring(prefix or Gunsmith.LocalizationPrefix or "gunsmith.framework") .. "." .. tostring(suffix or "")
end

function Core.FrameworkLocalizationKey(suffix)
    return Core.LocalizationKey(Gunsmith.LocalizationPrefix or "gunsmith.framework", suffix)
end

function Core.LocalizationKeyForItem(item, suffix)
    return Core.LocalizationKey(Core.LocalizationPrefixForItem(item), suffix)
end

function Core.LocalizationKeyForPlatform(platform, suffix)
    return Core.LocalizationKey(Core.LocalizationPrefixForPlatform(platform), suffix)
end

function Core.PathNameKey(platform, path)
    if platform and platform.pathNameKeys and platform.pathNameKeys[path] then
        return platform.pathNameKeys[path]
    end
    return Core.LocalizationKeyForPlatform(platform, "path." .. tostring(path))
end

function Core.EncodePreview(item, platform)
    local weapon = Core.WeaponConfig(item) or {}
    local preview = weapon.preview or {}
    local offset = preview.offset or { x = 0, y = 0 }
    return string.format(
        "padding=%.4f,scale=%.4f,offsetX=%.4f,offsetY=%.4f",
        preview.padding or 12,
        preview.scale or 1.0,
        offset.x or 0,
        offset.y or 0)
end

function Core.EncodeText(value)
    return tostring(value or "")
        :gsub("%%", "%%25")
        :gsub(":", "%%3A")
        :gsub("|", "%%7C")
        :gsub(",", "%%2C")
        :gsub(";", "%%3B")
        :gsub("~", "%%7E")
        :gsub("=", "%%3D")
end

function Core.JoinPath(parentPath, path)
    if not parentPath or parentPath == "" then return path end
    return parentPath .. "/" .. path
end

function Core.ParentPath(path)
    if not path or path == "" then return "" end
    local parent = string.match(path, "^(.*)/[^/]+$")
    return parent or ""
end

function Core.LeafPath(path)
    if not path or path == "" then return "" end
    return string.match(path, "([^/]+)$") or path
end

local function clearDescendants(selection, slotPath)
    local prefix = slotPath .. "/"
    for path, _ in pairs(selection) do
        if string.sub(path, 1, #prefix) == prefix then
            selection[path] = nil
        end
    end
end

local rootSlotDefsCache = setmetatable({}, { __mode = "k" })
local rootSlotDefByPathCache = setmetatable({}, { __mode = "k" })
local mountByParentPartCache = setmetatable({}, { __mode = "k" })
local requiredSlotSetByPlatformCache = setmetatable({}, { __mode = "k" })
local hiddenHomeRootPathCache = setmetatable({}, { __mode = "k" })
local noHiddenHomeRootPath = {}

function Core.PartExcludes(part)
    if not part or type(part.excludes) ~= "table" then return {} end
    return part.excludes
end

local function partExcludesPartId(part, excludedPartId)
    if not excludedPartId then return false end
    for _, partId in ipairs(Core.PartExcludes(part)) do
        if partId == excludedPartId then return true end
    end
    return false
end

local function pruneExcludedSelections(selection)
    if type(selection) ~= "table" then return false end

    local removePaths = {}
    for sourcePath, sourcePartId in pairs(selection) do
        local sourcePart = Core.GetPart(sourcePartId)
        for _, excludedPartId in ipairs(Core.PartExcludes(sourcePart)) do
            for targetPath, targetPartId in pairs(selection) do
                if targetPath ~= sourcePath and targetPartId == excludedPartId then
                    removePaths[targetPath] = true
                end
            end
        end
    end

    local changed = false
    for path, _ in pairs(removePaths) do
        if selection[path] ~= nil then
            selection[path] = nil
            clearDescendants(selection, path)
            changed = true
        end
    end
    return changed
end

function Core.RootSlotDef(platform, path)
    if not platform or not path then return nil end

    local byPath = rootSlotDefByPathCache[platform]
    if not byPath then
        byPath = {}
        for _, entry in ipairs(Core.RootSlotDefs(platform)) do
            byPath[entry.path] = entry
        end
        rootSlotDefByPathCache[platform] = byPath
    end

    return byPath[path]
end

local function partProvidesAccepted(part, accepts)
    if type(part) ~= "table" or type(part.provides) ~= "table" or type(accepts) ~= "table" then return false end
    for _, provided in ipairs(part.provides) do
        for _, accepted in ipairs(accepts) do
            if provided == accepted then return true end
        end
    end
    return false
end

function Core.ApplyMountDefaultsForPath(selection, path, visited, depth)
    if not selection or not path or path == "" then return end
    if depth and depth > 32 then return end
    visited = visited or {}

    local parentPart = Core.GetInstalledPart(selection, path)
    if not parentPart or type(parentPart.mounts) ~= "table" then return end

    for _, mount in ipairs(parentPart.mounts) do
        local childPathSegment = mount.path
        local partId = mount.defaultPart
        local childPath = Core.JoinPath(path, childPathSegment)
        local visitKey = childPath .. ":" .. tostring(partId)
        if type(partId) == "string" and partId ~= "" and not visited[visitKey] then
            visited[visitKey] = true
            local childPart = Core.GetPart(partId)
            if childPart and mount and childPart.type == (mount.partType or childPathSegment) and partProvidesAccepted(childPart, mount.accepts) then
                if not selection[childPath] then
                    selection[childPath] = partId
                end
                Core.ApplyMountDefaultsForPath(selection, childPath, visited, (depth or 0) + 1)
            end
        end
    end
end

function Core.RootConfig(weapon, path)
    if not weapon or type(weapon.roots) ~= "table" then return nil end
    local root = weapon.roots[path]
    return type(root) == "table" and root or nil
end

function Core.RootPartId(weapon, path)
    local root = Core.RootConfig(weapon, path)
    return root and root.part or nil
end

function Core.RootSocket(weapon, path)
    local root = Core.RootConfig(weapon, path)
    return root and root.socket or nil
end

local function copySelection(selection)
    local copy = {}
    for path, partId in pairs(selection or {}) do
        copy[path] = partId
    end
    return copy
end

function Core.RootSlotDefs(platform)
    if not platform then return {} end

    local cached = rootSlotDefsCache[platform]
    if cached then return cached end

    local result = {}
    if type(platform.rootSlots) == "table" then
        for _, entry in ipairs(platform.rootSlots) do
            if type(entry) == "table" and type(entry.path) == "string" then
                table.insert(result, entry)
            end
        end
    end
    rootSlotDefsCache[platform] = result
    return result
end

local defaultSelectionCache = {}

function Core.BuildDefaultSelection(platform, weapon)
    if not platform or not weapon or type(weapon.roots) ~= "table" then return {} end
    local cached = defaultSelectionCache[weapon]
    if cached then
        return copySelection(cached)
    end
    local selection = {}
    for _, root in ipairs(Core.RootSlotDefs(platform)) do
        local path = root.path
        local partId = Core.RootPartId(weapon, path)
        local part = Core.GetPart(partId)
        if part and part.type == path then
            selection[path] = partId
            Core.ApplyMountDefaultsForPath(selection, path, {}, 0)
        end
    end
    while pruneExcludedSelections(selection) do end
    defaultSelectionCache[weapon] = selection
    return copySelection(selection)
end

function Core.GetPart(partId)
    if not partId or partId == "" or partId == Gunsmith.EmptyPartId then return nil end
    return Gunsmith.Config.parts[partId]
end

function Core.GetInstalledPart(selection, path)
    return Core.GetPart(selection[path])
end

local function visualScale(visual)
    if type(visual) == "table" and type(visual.scale) == "number" and visual.scale > 0 then
        return visual.scale
    end
    return 1.0
end

local resolveDrawOffset

function Core.ResolveMountAnchor(selection, platform, weapon, path)
    local mount = Core.MountForPath(selection, path)
    local anchor = mount and mount.anchor or nil
    if not anchor then return nil end

    local parentPath = Core.ParentPath(path)
    local parentPart = Core.GetInstalledPart(selection, parentPath)
    local parentVisual = Core.PartVisual(parentPart)
    if parentVisual then
        local parentOffset = resolveDrawOffset(selection, platform, weapon, parentPath, parentVisual)
        if parentOffset then
            local parentAttachPoint = parentVisual.attachPoint or { x = 0, y = 0 }
            local parentScale = visualScale(parentVisual)
            return {
                x = parentOffset.x + (parentAttachPoint.x + anchor.x) * parentScale,
                y = parentOffset.y + (parentAttachPoint.y + anchor.y) * parentScale
            }
        end
    end

    local parentAnchor = nil
    if Core.IsRootSlot(platform, parentPath) then
        parentAnchor = Core.RootSocket(weapon, parentPath)
    else
        parentAnchor = Core.ResolveMountAnchor(selection, platform, weapon, parentPath)
    end

    if parentAnchor then
        return {
            x = parentAnchor.x + anchor.x,
            y = parentAnchor.y + anchor.y
        }
    end

    return nil
end

resolveDrawOffset = function(selection, platform, weapon, path, visual)
    local anchor = nil
    if Core.IsRootSlot(platform, path) then
        local rootPath = Core.LeafPath(path)
        anchor = Core.RootSocket(weapon, rootPath)
    else
        anchor = Core.ResolveMountAnchor(selection, platform, weapon, path)
    end

    if anchor and visual.attachPoint then
        local scale = visualScale(visual)
        return {
            x = anchor.x - visual.attachPoint.x * scale,
            y = anchor.y - visual.attachPoint.y * scale
        }
    end

    if anchor and visual.relativeOffset then
        return {
            x = anchor.x + visual.relativeOffset.x,
            y = anchor.y + visual.relativeOffset.y
        }
    end

    return nil
end

function Core.ResolveDrawOffset(selection, platform, weapon, path, visual)
    return resolveDrawOffset(selection, platform, weapon, path, visual)
end

local quickSlotsCache = setmetatable({}, { __mode = "k" })

function Core.InvalidateQuickSlotsCache(item)
    if item then quickSlotsCache[item] = nil end
end

function Core.QuickSlotsForSelection(item, selection, platform)
    local weapon = Core.WeaponConfig(item)
    if not weapon or type(selection) ~= "table" or type(platform) ~= "table" then return {} end

    local cached = quickSlotsCache[item]
    if cached and cached.selection == selection then
        return cached.slots
    end

    local bindings = weapon.quickSlotBindings
    if type(bindings) ~= "table" then
        if type(weapon.quickSlots) == "table" then return weapon.quickSlots end
        return {}
    end

    local slots = {}
    local paths = {}
    for path, _ in pairs(selection) do
        table.insert(paths, path)
    end
    table.sort(paths)

    for _, parentPath in ipairs(paths) do
        local parentPart = Core.GetInstalledPart(selection, parentPath)
        if type(parentPart) == "table" and type(parentPart.mounts) == "table" then
            for _, mount in ipairs(parentPart.mounts) do
                local quick = mount.quick
                if type(quick) == "table" and type(quick.key) == "string" and quick.key ~= "" then
                    local binding = bindings[quick.key]
                    local slotIndex = type(binding) == "table" and tonumber(binding.slot) or nil
                    local slotPath = Core.JoinPath(parentPath, mount.path)
                    if slotIndex and Core.IsValidPath(selection, platform, slotPath) then
                        local anchor = Core.ResolveMountAnchor(selection, platform, weapon, slotPath)
                        table.insert(slots, {
                            key = quick.key,
                            path = slotPath,
                            slot = slotIndex,
                            nameKey = quick.nameKey or mount.nameKey or Core.PathNameKey(platform, mount.path),
                            anchor = anchor,
                            itemPosOffset = binding.itemPosOffset,
                            showWhenContained = quick.showWhenContained,
                            rotation = binding.rotation
                        })
                    end
                end
            end
        end
    end

    table.sort(slots, function(left, right)
        if left.slot == right.slot then return left.path < right.path end
        return left.slot < right.slot
    end)
    quickSlotsCache[item] = { slots = slots, selection = selection }
    return slots
end

function Core.PartVisual(part)
    if not part then return nil end
    return part.visual
end

function Core.PartProvides(part)
    if not part or type(part.provides) ~= "table" then return {} end
    return part.provides
end

local partsByTypeCache = {}

function Core.GetPartsForType(partType)
    if not partType then return {} end

    local cached = partsByTypeCache[partType]
    if cached then return cached end

    local parts = {}
    for partId, part in pairs(Gunsmith.Config.parts) do
        if part.type == partType then
            table.insert(parts, partId)
        end
    end
    table.sort(parts)
    partsByTypeCache[partType] = parts
    return parts
end

function Core.PartTypeForPath(selection, path)
    local mount = Core.MountForPath(selection, path)
    return mount and mount.partType or Core.LeafPath(path)
end

local function contains(values, target)
    if type(values) ~= "table" then return false end
    for _, value in ipairs(values) do
        if value == target then return true end
    end
    return false
end

local function intersects(left, right)
    if type(left) ~= "table" or type(right) ~= "table" then return false end
    for _, value in ipairs(left) do
        if contains(right, value) then return true end
    end
    return false
end

local function pathWithin(path, rootPath)
    if type(path) ~= "string" or type(rootPath) ~= "string" or rootPath == "" then return false end
    return path == rootPath or string.sub(path, 1, #rootPath + 1) == rootPath .. "/"
end

local function partsMutuallyExclude(partId, part, otherPartId, otherPart)
    return partExcludesPartId(part, otherPartId) or partExcludesPartId(otherPart, partId)
end

function Core.PartConflictsWithSelection(selection, path, partId)
    if type(selection) ~= "table" then return false end
    local part = Core.GetPart(partId)
    if not part then return false end

    for selectedPath, selectedPartId in pairs(selection) do
        if not pathWithin(selectedPath, path) then
            local selectedPart = Core.GetPart(selectedPartId)
            if selectedPart and partsMutuallyExclude(partId, part, selectedPartId, selectedPart) then
                return true
            end
        end
    end
    return false
end

function Core.AcceptsForPath(selection, platform, path)
    if not platform or not path or path == "" then return nil end
    if Core.IsRootSlot(platform, path) then
        return nil
    end

    local mount = Core.MountForPath(selection, path)
    return mount and mount.accepts or nil
end

function Core.MountForPath(selection, path)
    if not selection or not path or path == "" then return nil end
    local parent = Core.ParentPath(path)
    local childPath = Core.LeafPath(path)
    local parentPart = Core.GetInstalledPart(selection, parent)
    if not parentPart or not parentPart.mounts then return nil end

    local byPath = mountByParentPartCache[parentPart]
    if not byPath then
        byPath = {}
        for _, mount in ipairs(parentPart.mounts) do
            if type(mount.path) == "string" then
                byPath[mount.path] = mount
            end
        end
        mountByParentPartCache[parentPart] = byPath
    end

    return byPath[childPath]
end

local function requiredSlotSet(platform)
    if not platform then return {} end

    local cached = requiredSlotSetByPlatformCache[platform]
    if cached then return cached end

    local set = {}
    if type(platform.requiredSlots) == "table" then
        for _, path in ipairs(platform.requiredSlots) do
            if type(path) == "string" then
                set[path] = true
            end
        end
    end
    requiredSlotSetByPlatformCache[platform] = set
    return set
end

function Core.IsRequiredSlot(platform, path)
    if not platform or not path or path == "" then return false end
    if not string.find(path, "/", 1, true) then
        return Core.RootSlotDef(platform, path) ~= nil
    end
    if platform.requiredSlots then
        local hiddenRootPath = Core.HiddenHomeRootPath(platform)
        local relativePath = path
        if hiddenRootPath then
            local prefix = hiddenRootPath .. "/"
            if string.sub(path, 1, #prefix) == prefix then
                relativePath = string.sub(path, #prefix + 1)
            end
        end
        return requiredSlotSet(platform)[relativePath] == true
    end
    return false
end

function Core.HiddenHomeRootPath(platform)
    if not platform then return nil end

    local cached = hiddenHomeRootPathCache[platform]
    if cached ~= nil then
        return cached ~= noHiddenHomeRootPath and cached or nil
    end

    local hiddenPath = nil
    for _, root in ipairs(Core.RootSlotDefs(platform)) do
        if root.hidden == true then
            if hiddenPath ~= nil then
                hiddenHomeRootPathCache[platform] = noHiddenHomeRootPath
                return nil
            end
            hiddenPath = root.path
        end
    end

    hiddenHomeRootPathCache[platform] = hiddenPath or noHiddenHomeRootPath
    return hiddenPath
end

function Core.IsPartCompatible(selection, platform, path, partId)
    local part = Core.GetPart(partId)
    if not part or not platform or not path or path == "" then return false end
    if part.type ~= Core.PartTypeForPath(selection, path) then return false end
    if Core.PartConflictsWithSelection(selection, path, partId) then return false end
    if Core.IsRootSlot(platform, path) then return true end

    local accepts = Core.AcceptsForPath(selection, platform, path)
    if type(accepts) ~= "table" then return false end
    return intersects(accepts, Core.PartProvides(part))
end

function Core.IsHiddenRootSlot(platform, path)
    local root = Core.RootSlotDef(platform, path)
    return root and root.hidden == true
end

function Core.NormalizeUiPath(platform, path)
    if not path or path == "" then return "" end
    if Core.IsHiddenRootSlot(platform, path) then return "" end
    return path
end

function Core.UiParentPath(platform, path)
    local parent = Core.ParentPath(path)
    return Core.NormalizeUiPath(platform, parent)
end

function Core.RootSlots(platform)
    local slots = {}
    for _, root in ipairs(Core.RootSlotDefs(platform)) do
        local path = root.path
        if not Core.IsHiddenRootSlot(platform, path) then
            table.insert(slots, { path = path, partType = path, nameKey = Core.PathNameKey(platform, path) })
        end
    end
    return slots
end

function Core.ChildSlots(selection, platform, path)
    local parentPart = Core.GetInstalledPart(selection, path)
    local slots = {}
    if not parentPart or not parentPart.mounts then return slots end

    for _, mount in ipairs(parentPart.mounts) do
        table.insert(slots, {
            path = Core.JoinPath(path, mount.path),
            partType = mount.partType or mount.path,
            nameKey = mount.nameKey or Core.PathNameKey(platform, mount.path)
        })
    end
    return slots
end

function Core.SlotsForPath(selection, platform, path)
    if not path or path == "" then
        local slots = Core.RootSlots(platform)
        for _, root in ipairs(Core.RootSlotDefs(platform)) do
            local rootPath = root.path
            if Core.IsHiddenRootSlot(platform, rootPath) then
                for _, childPath in ipairs(Core.ChildSlots(selection, platform, rootPath)) do
                    table.insert(slots, childPath)
                end
            end
        end
        return slots
    end
    return Core.ChildSlots(selection, platform, path)
end

function Core.HasChildSlots(selection, platform, path)
    return #Core.ChildSlots(selection, platform, path) > 0
end

function Core.IsRootSlot(platform, path)
    if not path or string.find(path, "/", 1, true) then return false end
    return Core.RootSlotDef(platform, path) ~= nil
end

function Core.IsValidPath(selection, platform, path)
    if Core.IsRootSlot(platform, path) then return true end
    local parent = Core.ParentPath(path)
    local childPath = Core.LeafPath(path)
    local parentPart = Core.GetInstalledPart(selection, parent)
    if not parentPart or not parentPart.mounts then return false end

    for _, mount in ipairs(parentPart.mounts) do
        if mount.path == childPath then return true end
    end
    return false
end

function Core.PruneInvalidSelections(selection, platform, weapon)
    local defaults = Core.BuildDefaultSelection(platform, weapon)
    local changed = true
    while changed do
        changed = false
        if pruneExcludedSelections(selection) then
            changed = true
        end
        for path, partId in pairs(selection) do
            if not Core.IsValidPath(selection, platform, path) or not Core.IsPartCompatible(selection, platform, path, partId) then
                local defaultPartId = defaults[path]
                if defaultPartId and partId ~= defaultPartId and Core.IsPartCompatible(selection, platform, path, defaultPartId) then
                    selection[path] = defaultPartId
                else
                    selection[path] = nil
                end
                changed = true
            end
        end
    end
end

function Core.ClearDescendants(selection, slotPath)
    clearDescendants(selection, slotPath)
end

function Core.SortedSelectionPaths(selection)
    local paths = {}
    for path, _ in pairs(selection) do
        table.insert(paths, path)
    end
    table.sort(paths)
    return paths
end

function Core.PathLabel(selection, platform, path)
    if not path or path == "" then
        local hiddenRootPath = Core.HiddenHomeRootPath(platform)
        if hiddenRootPath then
            return Core.FrameworkLocalizationKey("ui.weapon_root") .. ">" .. Core.PathNameKey(platform, hiddenRootPath)
        end
        return Core.FrameworkLocalizationKey("ui.weapon_root")
    end

    local names = { Core.FrameworkLocalizationKey("ui.weapon_root") }
    local current = ""
    for segment in string.gmatch(path, "[^/]+") do
        current = Core.JoinPath(current, segment)
        local mountNameKey = Core.PathNameKey(platform, segment)
        local parent = Core.ParentPath(current)
        local parentPart = parent ~= "" and Core.GetInstalledPart(selection, parent) or nil
        if parentPart and parentPart.mounts then
            for _, mount in ipairs(parentPart.mounts) do
                if mount.path == segment then
                    mountNameKey = mount.nameKey or mountNameKey
                    break
                end
            end
        end
        table.insert(names, mountNameKey)
    end
    return table.concat(names, " > ")
end
