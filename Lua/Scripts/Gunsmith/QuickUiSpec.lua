GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = Gunsmith.Core
local Inventory = Gunsmith.Inventory
local Stats = Gunsmith.Stats
local QuickMod = Gunsmith.QuickMod
local QuickUiSpec = {}
Gunsmith.QuickUiSpec = QuickUiSpec

local function canQuickSlotAccept(item, quickSlotIndex, identifier)
    if not QuickMod or quickSlotIndex == nil then return true end
    return QuickMod.CanSlotAcceptItemIdentifier(item, quickSlotIndex, identifier)
end

local function quickSlotHasItem(item, quickSlotIndex)
    if not QuickMod or not QuickMod.HasSlotItem or quickSlotIndex == nil then return false end
    return QuickMod.HasSlotItem(item, quickSlotIndex) == true
end

local function appendPartEntry(entries, item, selection, platform, slotPath, partId, quickSlotIndex, availableIds, ownerId)
    local part = Gunsmith.Config.parts[partId]
    if not part then return end

    local identifier = part.item and part.item.identifier or nil
    local status = "available"
    local replacingExisting = quickSlotHasItem(item, quickSlotIndex)
    if selection[slotPath] == partId then
        status = "installed"
    elseif not Core.IsPartCompatible(selection, platform, slotPath, partId, ownerId) then
        status = "incompatible"
    elseif identifier and identifier ~= "" and not replacingExisting and not canQuickSlotAccept(item, quickSlotIndex, identifier) then
        status = "incompatible"
    elseif availableIds and identifier and identifier ~= "" and not availableIds[identifier] then
        status = "missing"
    end

    table.insert(entries, Gunsmith.UiSpec.EncodePartEntry(partId, part, status))
end

local function quickSlotsForItem(item, selection, platform)
    if Core.QuickSlotsForSelection then
        return Core.QuickSlotsForSelection(item, selection, platform)
    end

    local weapon = Core.WeaponConfig(item)
    if not weapon or type(weapon.quickSlots) ~= "table" then return {} end
    local slots = {}
    for _, quickSlot in ipairs(weapon.quickSlots) do
        if quickSlot.path and quickSlot.slot then
            table.insert(slots, quickSlot)
        end
    end
    return slots
end

local function compatibleItemIdentifiers(item, selection, platform, slotPath, quickSlotIndex, ownerId)
    local identifiers = {}
    local seen = {}
    local partType = Core.PartTypeForPath(selection, slotPath)
    local replacingExisting = quickSlotHasItem(item, quickSlotIndex)
    for _, partId in ipairs(Core.GetPartsForType(partType, ownerId)) do
        local part = Gunsmith.Config.parts[partId]
        local identifier = part and part.item and part.item.identifier or nil
        if identifier and identifier ~= "" and not seen[identifier] and
            Core.IsPartCompatible(selection, platform, slotPath, partId, ownerId) and
            (replacingExisting or canQuickSlotAccept(item, quickSlotIndex, identifier)) then
            seen[identifier] = true
            table.insert(identifiers, identifier)
        end
    end
    return table.concat(identifiers, "~")
end

local function quickMeta(item, selection, platform, weapon, quickSlot, ownerId)
    local anchor = quickSlot.anchor or Core.ResolveMountAnchor(selection, platform, weapon, quickSlot.path)
    local valid = anchor and "1" or "0"
    anchor = anchor or { x = 0, y = 0 }
    return string.format(
        "slot=%d,anchorX=%.4f,anchorY=%.4f,anchorValid=%s,items=%s",
        quickSlot.slot,
        anchor.x or 0,
        anchor.y or 0,
        valid,
        compatibleItemIdentifiers(item, selection, platform, quickSlot.path, quickSlot.slot, ownerId))
end

local buildCache = setmetatable({}, { __mode = "k" })

function QuickUiSpec.Build(item, selection, platform)
    local cached = buildCache[item]
    if cached and cached.selection == selection then
        return cached.spec
    end

    local weapon = Core.WeaponConfig(item)
    local ownerId = Core.OwnerForWeapon(weapon)
    local character = Inventory and Inventory.ActorForItem(item) or nil
    local availableIds = Inventory and Inventory.CollectAvailableItemIds(character, item) or {}
    local entries = {}

    for _, quickSlot in ipairs(quickSlotsForItem(item, selection, platform)) do
        if Core.IsValidPath(selection, platform, quickSlot.path) then
            local partType = Core.PartTypeForPath(selection, quickSlot.path)
            local emptyStatus = "available"
            if Core.IsRequiredSlot(platform, quickSlot.path) then
                emptyStatus = "disabled"
            elseif not selection[quickSlot.path] then
                emptyStatus = "installed"
            end

            local partEntries = { Gunsmith.EmptyPartId .. ":" .. Core.FrameworkLocalizationKey("ui.empty_part") .. ":" .. emptyStatus }
            for _, partId in ipairs(Core.GetPartsForType(partType, ownerId)) do
                appendPartEntry(partEntries, item, selection, platform, quickSlot.path, partId, quickSlot.slot, availableIds, ownerId)
            end

            table.insert(entries, table.concat({
                quickSlot.path,
                quickSlot.nameKey or Core.PathNameKey(platform, quickSlot.path),
                tostring(selection[quickSlot.path] or ""),
                "0",
                table.concat(partEntries, ","),
                quickMeta(item, selection, platform, weapon, quickSlot, ownerId)
            }, "|"))
        end
    end

    local spec = table.concat({
        "",
        Core.FrameworkLocalizationKey("ui.quick_root"),
        ""
    }, "|") .. "::" .. Core.EncodePreview(item, platform) .. "::" .. Stats.Encode(Stats.SumSelection(selection)) .. "::" .. table.concat(entries, ";")
    buildCache[item] = { spec = spec, selection = selection }
    return spec
end

function QuickUiSpec.InvalidateCache(item)
    if item then buildCache[item] = nil end
end
