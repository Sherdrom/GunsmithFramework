GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = Gunsmith.Core
local Inventory = Gunsmith.Inventory
local Stats = Gunsmith.Stats
local UiSpec = {}
Gunsmith.UiSpec = UiSpec

function UiSpec.EncodePartEntry(partId, part, status)
    local visual = part.visual or {}
    local source = visual.source or {}
    return table.concat({
        partId,
        part.nameKey,
        status,
        Stats.Encode(Stats.PartStats(part), "~"),
        Core.EncodeText(part.item and part.item.identifier or ""),
        Core.EncodeText(visual.texture or ""),
        (Core.EncodeText(string.format("%d,%d,%d,%d", source.x or 0, source.y or 0, source.w or 0, source.h or 0)))
    }, ":")
end

local function appendPartEntry(entries, item, selection, platform, slotPath, partId, ownerId)
    local part = Gunsmith.Config.parts[partId]
    if part then
        local weapon = Core.WeaponConfig(item)
        local status = "available"
        if selection[slotPath] == partId then
            status = "installed"
        elseif not Core.IsPartCompatible(selection, platform, slotPath, partId, ownerId) then
            status = "incompatible"
        elseif Inventory and not Inventory.HasPartItem(Inventory.ActorForItem(item), part, item) then
            status = "missing"
        end
        table.insert(entries, UiSpec.EncodePartEntry(partId, part, status))
    end
end

function UiSpec.Build(item, selection, platform, currentPath)
    local path = Core.NormalizeUiPath(platform, currentPath or "")
    local ownerId = Core.OwnerForWeaponId(Core.ItemIdentifier(item))
    local entries = {}

    for _, slot in ipairs(Core.SlotsForPath(selection, platform, path)) do
        local emptyStatus = "available"
        if Core.IsRequiredSlot(platform, slot.path) then
            emptyStatus = "disabled"
        elseif not selection[slot.path] then
            emptyStatus = "installed"
        end
        local partEntries = { Gunsmith.EmptyPartId .. ":" .. Core.FrameworkLocalizationKey("ui.empty_part") .. ":" .. emptyStatus }
        for _, partId in ipairs(Core.GetPartsForType(slot.partType, ownerId)) do
            appendPartEntry(partEntries, item, selection, platform, slot.path, partId, ownerId)
        end

        local slotPath = slot.path
        local currentPartId = tostring(selection[slotPath] or "")
        local canEnter = Core.HasChildSlots(selection, platform, slotPath) and "1" or "0"
        table.insert(entries, table.concat({
            slotPath,
            slot.nameKey,
            currentPartId,
            canEnter,
            table.concat(partEntries, ",")
        }, "|"))
    end

    return table.concat({
        path,
        Core.PathLabel(selection, platform, path),
        Core.UiParentPath(platform, path)
    }, "|") .. "::" .. Core.EncodePreview(item, platform) .. "::" .. Stats.Encode(Stats.SumSelection(selection)) .. "::" .. table.concat(entries, ";")
end
