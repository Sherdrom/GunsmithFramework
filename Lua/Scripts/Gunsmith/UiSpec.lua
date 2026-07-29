GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = Gunsmith.Core
local Inventory = Gunsmith.Inventory
local Stats = Gunsmith.Stats
local QuickMod = Gunsmith.QuickMod
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
        local emptyPartId = Core.EmptyPartForPath(selection, slot.path)
        local emptyStatus = "available"
        if not selection[slot.path] or selection[slot.path] == emptyPartId then
            emptyStatus = "installed"
        end
        local emptyNameKey = Core.FrameworkLocalizationKey(
            Core.IsRequiredSlot(platform, slot.path) and "ui.empty_required_part" or "ui.empty_part")
        local partEntries = { Gunsmith.EmptyPartId .. ":" .. emptyNameKey .. ":" .. emptyStatus }
        local includedPartIds = {}
        for _, partId in ipairs(Core.GetPartsForType(slot.partType, ownerId)) do
            if partId ~= emptyPartId then
                includedPartIds[partId] = true
                appendPartEntry(partEntries, item, selection, platform, slot.path, partId, ownerId)
            end
        end
        local currentPartId = selection[slot.path]
        if currentPartId and currentPartId ~= emptyPartId and not includedPartIds[currentPartId] then
            appendPartEntry(partEntries, item, selection, platform, slot.path, currentPartId, ownerId)
        end

        local slotPath = slot.path
        local entry = {
            slotPath,
            slot.nameKey,
            tostring(currentPartId == emptyPartId and "" or currentPartId or ""),
            Core.HasChildSlots(selection, platform, slotPath) and "1" or "0",
            table.concat(partEntries, ",")
        }
        local quickSlotIndex = QuickMod and QuickMod.SlotForPath(item, slotPath) or nil
        if quickSlotIndex ~= nil then
            table.insert(entry, "slot=" .. tostring(quickSlotIndex))
        end
        table.insert(entries, table.concat(entry, "|"))
    end

    return table.concat({
        path,
        Core.PathLabel(selection, platform, path),
        Core.UiParentPath(platform, path)
    }, "|") .. "::" .. Core.EncodePreview(item, platform) .. "::" .. Stats.Encode(Stats.SumSelection(selection)) .. "::" .. table.concat(entries, ";")
end
