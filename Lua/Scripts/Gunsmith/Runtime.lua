GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = Gunsmith.Core
local Persistence = Gunsmith.Persistence
local UiSpec = Gunsmith.UiSpec
local QuickUiSpec = Gunsmith.QuickUiSpec
local Inventory = Gunsmith.Inventory
local Stats = Gunsmith.Stats
local QuickMod = Gunsmith.QuickMod
local Runtime = {}

Gunsmith.Runtime = Runtime
Gunsmith.EmptyPartId = "__empty"
Gunsmith.State = Gunsmith.State or {
    selections = {},
    appliedSignatures = {},
    appliedConfigSignatures = {},
    uiPaths = {},
    loadedStates = {}
}

local State = Gunsmith.State
State.appliedSignatures = State.appliedSignatures or {}
State.appliedConfigSignatures = State.appliedConfigSignatures or {}
State.savedSignatures = State.savedSignatures or {}
State.pendingPartsRefresh = State.pendingPartsRefresh or {}
State.initialQuickItemsEnsured = State.initialQuickItemsEnsured or {}
setmetatable(State.appliedSignatures, { __mode = "k" })
setmetatable(State.appliedConfigSignatures, { __mode = "k" })
setmetatable(State.savedSignatures, { __mode = "k" })
setmetatable(State.pendingPartsRefresh, { __mode = "k" })
setmetatable(State.initialQuickItemsEnsured, { __mode = "k" })
local finishQuickModChange
local saveSelectionIfChanged

local function isGunsmithUiOpen(item, mode)
    if not Hook or not Hook.Call or not item then return false end
    return Hook.Call("GunsmithFrameworkIsOpen", item, mode or "") == true
end

local function invalidateAppliedState(item)
    if not item then return end
    State.appliedSignatures[item] = nil
    State.appliedConfigSignatures[item] = nil
end

local function applyServerSavedSelection(item, selection, platform, weapon)
    if not SERVER or not Hook or not Hook.Call then return end
    local savedState = Hook.Call("GunsmithFrameworkGetSavedState", item)
    if type(savedState) ~= "string" or savedState == "" then return end

    local ownerId = Core.OwnerForWeapon(weapon)
    Persistence.ApplySavedParts(selection, platform, weapon, Persistence.Decode(savedState), ownerId)
    Core.PruneInvalidSelections(selection, platform, weapon, ownerId)
end

function Runtime.GetSelection(item)
    local platform = Core.PlatformConfig(item)
    if not platform then return nil end

    local key = Core.ItemKey(item)
    if not State.selections[key] then
        local weapon = Core.WeaponConfig(item)
        State.selections[key] = Core.BuildDefaultSelection(platform, weapon, Core.OwnerForWeapon(weapon))
        if not State.loadedStates[key] then
            State.loadedStates[key] = true
            if SERVER then
                applyServerSavedSelection(item, State.selections[key], platform, weapon)
            else
                Persistence.Request(item)
            end
        end
    end
    return State.selections[key]
end

function Runtime.GetCurrentUiPath(item)
    local key = Core.ItemKey(item)
    if not key then return "" end
    return State.uiPaths[key] or ""
end

function Runtime.SetCurrentUiPath(item, path)
    local key = Core.ItemKey(item)
    if not key then return end
    State.uiPaths[key] = Core.NormalizeUiPath(Core.PlatformConfig(item), path or "")
end

function Runtime.SchedulePartsRefresh(item, delay, alreadySynced)
    if SERVER then return end
    if not item or item.removed then return end
    local pending = State.pendingPartsRefresh[item]
    if pending then
        pending.alreadySynced = pending.alreadySynced and alreadySynced == true
        return
    end

    pending = { alreadySynced = alreadySynced == true }
    State.pendingPartsRefresh[item] = pending

    local function flush()
        local entry = State.pendingPartsRefresh[item]
        if not entry then return end
        State.pendingPartsRefresh[item] = nil
        if item and not item.removed then
            Runtime.RefreshParts(item, entry.alreadySynced)
        end
    end

    if Timer and Timer.Wait then
        Timer.Wait(flush, delay or 1)
    else
        flush()
    end
end

function Runtime.RefreshParts(item, alreadySynced)
    if SERVER then return end
    if not item or not Core.PlatformConfig(item) then return end

    local platform = Core.PlatformConfig(item)
    local selection = Runtime.GetSelection(item)
    if not Hook or not Hook.Call then
        print("[GunsmithFramework] Hook.Call is unavailable; cannot refresh C# gunsmith parts UI.")
        return
    end

    if not isGunsmithUiOpen(item, "parts") then return end

    if not alreadySynced and QuickMod and QuickMod.SyncFromContainer(item, selection, platform) then
        saveSelectionIfChanged(item, selection, platform, Core.WeaponConfig(item))
    end

    local currentPath = Runtime.GetCurrentUiPath(item)
    if currentPath ~= "" and #Core.SlotsForPath(selection, platform, currentPath) == 0 then
        currentPath = Core.UiParentPath(platform, currentPath)
        Runtime.SetCurrentUiPath(item, currentPath)
    end

    Hook.Call("GunsmithFrameworkRefreshParts", item, UiSpec.Build(item, selection, platform, currentPath))
end

function Runtime.RefreshQuick(item, alreadySynced)
    if SERVER then return end
    if not item or not Core.PlatformConfig(item) then return end

    local platform = Core.PlatformConfig(item)
    local selection = Runtime.GetSelection(item)
    if not Hook or not Hook.Call then
        print("[GunsmithFramework] Hook.Call is unavailable; cannot refresh C# gunsmith quick UI.")
        return
    end

    if not isGunsmithUiOpen(item, "quick") then return end

    if not alreadySynced and QuickMod and QuickMod.SyncFromContainer(item, selection, platform) then
        saveSelectionIfChanged(item, selection, platform, Core.WeaponConfig(item))
    end

    Hook.Call("GunsmithFrameworkRefreshQuick", item, QuickUiSpec.Build(item, selection, platform))
end

function Runtime.SyncQuickModContainerItem(item)
    if SERVER then return end
    if not QuickMod then return end
    if not item or item.removed then return end

    local platform = Core.PlatformConfig(item)
    if not platform then return end

    local selection = Runtime.GetSelection(item)
    if not selection then return end

    if QuickMod.SyncFromContainer(item, selection, platform, true) then
        finishQuickModChange(item, selection, platform, Core.WeaponConfig(item), true)
        Runtime.RefreshParts(item, true)
    end
end

function Runtime.SyncQuickContainer(item)
    if SERVER then return false end
    if not QuickMod then return false end
    if not item or item.removed then return false end

    local platform = Core.PlatformConfig(item)
    if not platform then return false end

    local selection = Runtime.GetSelection(item)
    if not selection then return false end

    if QuickMod.SyncFromContainer(item, selection, platform, true) then
        finishQuickModChange(item, selection, platform, Core.WeaponConfig(item), true)
    else
        local weapon = Core.WeaponConfig(item)
        Core.PruneInvalidSelections(selection, platform, weapon, Core.OwnerForWeapon(weapon))
    end

    Runtime.RefreshQuick(item, true)
    return true
end

finishQuickModChange = function(item, selection, platform, weapon, alreadySynced)
    if not alreadySynced then
        QuickMod.SyncFromContainer(item, selection, platform, true)
    end
    if not alreadySynced then
        Core.PruneInvalidSelections(selection, platform, weapon, Core.OwnerForWeapon(weapon))
    end
    saveSelectionIfChanged(item, selection, platform, weapon)
    Runtime.Apply(item, true)
end

local function buildSignature(item, selection, platform, skipPrune)
    if not skipPrune then
        local weapon = Core.WeaponConfig(item)
        Core.PruneInvalidSelections(selection, platform, weapon, Core.OwnerForWeapon(weapon))
    end
    local values = {}
    for _, path in ipairs(Core.SortedSelectionPaths(selection)) do
        table.insert(values, path .. ":" .. tostring(selection[path] or ""))
    end
    return table.concat(values, ",")
end

saveSelectionIfChanged = function(item, selection, platform, weapon)
    if not item or not selection or not platform then return false end
    local signature = buildSignature(item, selection, platform)
    if State.savedSignatures[item] == signature then
        return false
    end

    Persistence.Save(item)
    State.savedSignatures[item] = signature
    return true
end

local function buildLayerSpecForItem(item, selection, platform)
    local weapon = Core.WeaponConfig(item)
    local layers = {}
    for _, path in ipairs(Core.SortedSelectionPaths(selection)) do
        local part = Core.GetPart(selection[path])
        local visual = Core.PartVisual(part)
        if visual and visual.texture and visual.source then
            local source = visual.source
            local drawOffset = Core.ResolveDrawOffset(selection, platform, weapon, path, visual)
            if drawOffset then
                local scale = visual.scale or 1.0
                local mount = Core.MountForPath(selection, path)
                local order = mount and mount.visualOrder or visual.order or 0
                local itemIdentifier = ""
                if type(part) == "table" and type(part.item) == "table" and type(part.item.identifier) == "string" then
                    itemIdentifier = part.item.identifier
                end
                table.insert(layers, table.concat({
                    path,
                    selection[path],
                    itemIdentifier,
                    visual.texture,
                    string.format("%d,%d,%d,%d", source.x, source.y, source.w, source.h),
                    string.format("%.4f,%.4f", drawOffset.x, drawOffset.y),
                    tostring(order),
                    string.format("%.4f", scale)
                }, "|"))
            end
        end
    end
    return table.concat(layers, ";")
end

local function encodeInventorySettings(item)
    local weapon = Core.WeaponConfig(item) or {}
    local inventory = weapon.inventory or {}
    return string.format(
        "scale=%.4f,rotation=%.4f,padding=%.4f",
        inventory.scale or 1.0,
        inventory.rotation or 0.0,
        inventory.padding or 0.0)
end

local function encodeWorldSettings(item)
    local weapon = Core.WeaponConfig(item) or {}
    local world = weapon.world or {}
    local offset = world.offset or { x = 0, y = 0 }
    return string.format(
        "scale=%.4f,rotation=%.4f,padding=%.4f,offsetX=%.4f,offsetY=%.4f",
        world.scale or 1.0,
        world.rotation or 0.0,
        world.padding or 0.0,
        offset.x or 0.0,
        offset.y or 0.0)
end

local function encodeManagedItems(selection)
    return table.concat(Stats.ManagedItemIdentifiers(selection), ",")
end

local function registerQuickSlotLayouts(item, selection, platform, weapon)
    if not Hook or not Hook.Call then return end
    Hook.Call("GunsmithFrameworkClearQuickSlotLayouts", item)

    if not Core.QuickSlotsForSelection then return end
    for _, quickSlot in ipairs(Core.QuickSlotsForSelection(item, selection, platform)) do
        local anchor = quickSlot.anchor
        if anchor then
            local offset = quickSlot.itemPosOffset or {}
            Hook.Call(
                "GunsmithFrameworkRegisterQuickSlotLayout",
                item,
                quickSlot.slot,
                anchor.x or 0,
                anchor.y or 0,
                0,
                0,
                offset.x or 0,
                offset.y or 0,
                quickSlot.rotation or 0)
        end
    end
end

local function getPartQuickAttachmentTransform(part)
    if type(part) ~= "table" or type(part.quickAttachmentTransform) ~= "table" then return nil end
    return part.quickAttachmentTransform
end

local function isFiniteNumber(value)
    return type(value) == "number" and value == value and value ~= math.huge and value ~= -math.huge
end

local function requireFiniteNumber(value, label)
    local number = tonumber(value)
    if not isFiniteNumber(number) then
        error("[GunsmithFramework] " .. label .. " must be a finite number")
    end
    return number
end

local function getMuzzleOutletOffset(selection, path)
    local part = Core.GetInstalledPart(selection, path)
    local transform = getPartQuickAttachmentTransform(part)
    if not transform then return { x = 0, y = 0 } end
    if transform.muzzleOutletOffset == nil then return { x = 0, y = 0 } end
    if type(transform.muzzleOutletOffset) ~= "table" then
        error("[GunsmithFramework] quickAttachmentTransform.muzzleOutletOffset at " .. tostring(path) .. " must contain numeric x/y")
    end

    return {
        x = requireFiniteNumber(transform.muzzleOutletOffset.x, "quickAttachmentTransform.muzzleOutletOffset.x at " .. tostring(path)),
        y = requireFiniteNumber(transform.muzzleOutletOffset.y, "quickAttachmentTransform.muzzleOutletOffset.y at " .. tostring(path))
    }
end

local function hasMuzzleOutletOffset(selection, path)
    local part = Core.GetInstalledPart(selection, path)
    local transform = getPartQuickAttachmentTransform(part)
    return type(transform) == "table" and transform.muzzleOutletOffset ~= nil
end

local function findQuickSlotByKey(item, selection, platform, key)
    if not Core.QuickSlotsForSelection then return nil end
    for _, quickSlot in ipairs(Core.QuickSlotsForSelection(item, selection, platform)) do
        if quickSlot.key == key then
            return quickSlot
        end
    end
    return nil
end

local function registerQuickAttachmentBarrel(item, selection, platform, weapon, quickSlotKey, barrelKey)
    local quickSlot = findQuickSlotByKey(item, selection, platform, quickSlotKey)
    if not quickSlot or not quickSlot.anchor or type(quickSlot.path) ~= "string" then return false end
    local outletOffset = getMuzzleOutletOffset(selection, quickSlot.path)
    local world = weapon and weapon.world or {}
    local worldOffset = world.offset or {}
    local canvas = platform and platform.canvas or {}

    Hook.Call(
        "GunsmithFrameworkRegisterQuickAttachmentBarrelCanvasPoint",
        item,
        barrelKey,
        quickSlot.anchor.x or 0,
        quickSlot.anchor.y or 0,
        outletOffset.x or 0,
        outletOffset.y or 0,
        0,
        canvas.w or 0,
        canvas.h or 0,
        world.scale or 1.0,
        world.rotation or 0.0,
        worldOffset.x or 0.0,
        worldOffset.y or 0.0)
    return true
end

local function registerQuickAttachmentBarrels(item, selection, platform, weapon)
    if not Hook or not Hook.Call then return end
    Hook.Call("GunsmithFrameworkClearQuickAttachmentBarrelTransforms", item)

    registerQuickAttachmentBarrel(item, selection, platform, weapon, "muzzle", "primary")

    local lowerRailSlot = findQuickSlotByKey(item, selection, platform, "lower_rail")
    if lowerRailSlot and type(lowerRailSlot.path) == "string" and hasMuzzleOutletOffset(selection, lowerRailSlot.path) then
        registerQuickAttachmentBarrel(item, selection, platform, weapon, "lower_rail", "lower_rail")
    end
end

local function appendQuickAttachmentBarrelSignature(values, item, selection, platform, weapon, quickSlotKey, barrelKey)
    local quickSlot = findQuickSlotByKey(item, selection, platform, quickSlotKey)
    if not quickSlot or not quickSlot.anchor or type(quickSlot.path) ~= "string" then return end
    local outletOffset = getMuzzleOutletOffset(selection, quickSlot.path)
    local world = weapon and weapon.world or {}
    local worldOffset = world.offset or {}
    local canvas = platform and platform.canvas or {}

    table.insert(values, string.format(
        "%s:%s:anchor=%.4f,%.4f:outlet=%.4f,%.4f:canvas=%.4f,%.4f:world=%.4f,%.4f,%.4f,%.4f",
        barrelKey,
        quickSlot.path,
        quickSlot.anchor.x or 0,
        quickSlot.anchor.y or 0,
        outletOffset.x or 0,
        outletOffset.y or 0,
        canvas.w or 0,
        canvas.h or 0,
        world.scale or 1.0,
        world.rotation or 0.0,
        worldOffset.x or 0.0,
        worldOffset.y or 0.0))
end

local function buildQuickAttachmentBarrelSignature(item, selection, platform, weapon)
    local values = {}
    appendQuickAttachmentBarrelSignature(values, item, selection, platform, weapon, "muzzle", "primary")

    local lowerRailSlot = findQuickSlotByKey(item, selection, platform, "lower_rail")
    if lowerRailSlot and type(lowerRailSlot.path) == "string" and hasMuzzleOutletOffset(selection, lowerRailSlot.path) then
        appendQuickAttachmentBarrelSignature(values, item, selection, platform, weapon, "lower_rail", "lower_rail")
    end

    return table.concat(values, "|")
end

function Runtime.Apply(item, alreadySynced)
    if not item or item.removed then return end

    local platform = Core.PlatformConfig(item)
    if not platform then return end

    local selection = Runtime.GetSelection(item)
    if not selection then return end
    if QuickMod and QuickMod.EnsureSelectionItems and not State.initialQuickItemsEnsured[item] then
        State.initialQuickItemsEnsured[item] = QuickMod.EnsureSelectionItems(item, selection)
    end
    if not alreadySynced and QuickMod and QuickMod.SyncFromContainer(item, selection, platform) then
        if not SERVER then
            saveSelectionIfChanged(item, selection, platform, Core.WeaponConfig(item))
        end
    end

    local selectionSignature
    local weapon = Core.WeaponConfig(item)
    local quickAttachmentBarrelSpec
    if alreadySynced then
        selectionSignature = buildSignature(item, selection, platform, true)
        quickAttachmentBarrelSpec = buildQuickAttachmentBarrelSignature(item, selection, platform, weapon)
        local quickSignature = selectionSignature .. "|qatBarrels:" .. quickAttachmentBarrelSpec
        State.lastQuickSignatures = State.lastQuickSignatures or {}
        if not getmetatable(State.lastQuickSignatures) then
            setmetatable(State.lastQuickSignatures, { __mode = "k" })
        end
        if State.lastQuickSignatures[item] == quickSignature then
            return
        end
        State.lastQuickSignatures[item] = quickSignature
    end

    local inventorySpec = encodeInventorySettings(item)
    local worldSpec = encodeWorldSettings(item)
    if not selectionSignature then
        selectionSignature = buildSignature(item, selection, platform, false)
    end
    quickAttachmentBarrelSpec = quickAttachmentBarrelSpec or buildQuickAttachmentBarrelSignature(item, selection, platform, weapon)
    local configSignature = selectionSignature .. "|inventory:" .. inventorySpec .. "|world:" .. worldSpec .. "|qatBarrels:" .. quickAttachmentBarrelSpec
    if State.appliedConfigSignatures[item] == configSignature then
        return
    end

    if SERVER then
        local statsSpec = Stats.Encode(Stats.SumSelection(selection))
        local managedItemSpec = encodeManagedItems(selection)
        if Hook and Hook.Call then
            Hook.Call(
                "GunsmithFrameworkApplyRuntimeState",
                item,
                configSignature .. "|stats:" .. statsSpec .. "|items:" .. managedItemSpec,
                statsSpec,
                managedItemSpec)
        end
        registerQuickAttachmentBarrels(item, selection, platform, weapon)
        State.appliedConfigSignatures[item] = configSignature
        return
    end
    local statsSpec = Stats.Encode(Stats.SumSelection(selection))
    local managedItemSpec = encodeManagedItems(selection)
    local signature = configSignature .. "|stats:" .. statsSpec .. "|items:" .. managedItemSpec

    local layerSpec = buildLayerSpecForItem(item, selection, platform)
    if Hook and Hook.Call then
        registerQuickSlotLayouts(item, selection, platform, weapon)
        local applied = true
        if State.appliedSignatures[item] ~= signature then
            applied = Hook.Call("GunsmithFrameworkApply", item, signature, layerSpec, inventorySpec, worldSpec, statsSpec, managedItemSpec, platform.canvas.w, platform.canvas.h) == true
            if applied then
                State.appliedSignatures[item] = signature
            end
        end
        if applied then
            registerQuickAttachmentBarrels(item, selection, platform, weapon)
            State.appliedConfigSignatures[item] = configSignature
        end
    else
        print("[GunsmithFramework] Hook.Call is unavailable; cannot apply composed sprite.")
    end
end

function Runtime.EnsureApplied(item)
    if not item or item.removed then return end
    if not Core.WeaponConfig(item) then return end
    Runtime.Apply(item)
end

function Runtime.CyclePart(item, slotPath)
    local selection = Runtime.GetSelection(item)
    local platform = Core.PlatformConfig(item)
    local weapon = Core.WeaponConfig(item)
    local ownerId = Core.OwnerForWeapon(weapon)
    if not selection or not platform or not Core.IsValidPath(selection, platform, slotPath) then return end

    local parts = {}
    for _, partId in ipairs(Core.GetPartsForType(Core.PartTypeForPath(selection, slotPath), ownerId)) do
        if Core.IsPartCompatible(selection, platform, slotPath, partId, ownerId) then
            table.insert(parts, partId)
        end
    end
    if #parts == 0 then return end

    local current = selection[slotPath]
    local nextIndex = 1
    for index, partId in ipairs(parts) do
        if partId == current then
            nextIndex = index + 1
            break
        end
    end
    if nextIndex > #parts then nextIndex = 1 end

    return Runtime.SetPart(item, slotPath, parts[nextIndex])
end

local function selectionSubtreePaths(selection, slotPath)
    local paths = {}
    local prefix = slotPath .. "/"
    for path, _ in pairs(selection) do
        if path == slotPath or string.sub(path, 1, #prefix) == prefix then
            table.insert(paths, path)
        end
    end
    table.sort(paths, function(left, right)
        local _, leftDepth = string.gsub(left, "/", "")
        local _, rightDepth = string.gsub(right, "/", "")
        if leftDepth == rightDepth then return left > right end
        return leftDepth > rightDepth
    end)
    return paths
end

local function quickSlotsInSubtree(sourceItem, selection, platform, slotPath)
    local quickSlotsByPath = {}
    if not QuickMod or not Core.QuickSlotsForSelection then return quickSlotsByPath end

    local prefix = slotPath .. "/"
    for _, quickSlot in ipairs(Core.QuickSlotsForSelection(sourceItem, selection, platform)) do
        local path = quickSlot.path
        if type(path) == "string" and (path == slotPath or string.sub(path, 1, #prefix) == prefix) then
            quickSlotsByPath[path] = quickSlot.slot
        end
    end
    return quickSlotsByPath
end

local function returnSelectionSubtree(character, sourceItem, selection, platform, slotPath, onAllReturned)
    if not Inventory then return 0 end
    local returns = {}
    local quickReturnedPaths = {}
    local quickSlotsByPath = quickSlotsInSubtree(sourceItem, selection, platform, slotPath)

    for path, slotIndex in pairs(quickSlotsByPath) do
        if slotIndex ~= nil and QuickMod.HasSlotItem(sourceItem, slotIndex) then
            table.insert(returns, { quickSlotIndex = slotIndex })
            quickReturnedPaths[path] = true
        end
    end

    for _, path in ipairs(selectionSubtreePaths(selection, slotPath)) do
        local partId = selection[path]
        local part = Core.GetPart(partId)
        if part and Inventory.ItemIdentifierForPart(part) and not quickReturnedPaths[path] then
            table.insert(returns, { part = part })
        end
    end

    local pending = #returns
    if pending == 0 then return 0 end

    local completed = 0
    local notified = false
    local function notifyIfComplete()
        if not notified and pending > 0 and completed >= pending and onAllReturned then
            notified = true
            onAllReturned()
        end
    end

    local function onReturned()
        completed = completed + 1
        notifyIfComplete()
    end

    for _, returnSpec in ipairs(returns) do
        local queued = false
        if returnSpec.quickSlotIndex ~= nil and QuickMod then
            queued = QuickMod.ClearSlot(sourceItem, character, returnSpec.quickSlotIndex, onReturned) == true
        elseif returnSpec.part then
            queued = Inventory.ReturnPartItem(character, returnSpec.part, onReturned, sourceItem) == true
        end
        if not queued then
            pending = pending - 1
            notifyIfComplete()
        end
    end

    if pending == 0 then return 0 end
    return pending
end

function Runtime.SetPart(item, slotPath, partId, refreshMode, character)
    local selection = Runtime.GetSelection(item)
    local platform = Core.PlatformConfig(item)
    local weapon = Core.WeaponConfig(item)
    local ownerId = Core.OwnerForWeapon(weapon)
    if not selection or not platform or not Core.IsValidPath(selection, platform, slotPath) then return end

    character = character or (Inventory and Inventory.ActorForItem(item) or nil)

    local quickSlotIndex = QuickMod and QuickMod.SlotForPath(item, slotPath) or nil
    if quickSlotIndex ~= nil then
        local refreshQuick = refreshMode == "quick"
        local slotIndex = quickSlotIndex
        local refreshAfterReturn = function()
            if refreshQuick then
                Runtime.RefreshQuick(item)
            else
                Runtime.SchedulePartsRefresh(item, 0)
            end
        end

        if partId == Gunsmith.EmptyPartId then
            if Core.IsRequiredSlot(platform, slotPath) then return end
            if not QuickMod.ClearSlot(item, character, slotIndex, refreshAfterReturn) then return end
        else
            local part = Gunsmith.Config.parts[partId]
            if not part or not Core.IsPartCompatible(selection, platform, slotPath, partId, ownerId) then return end
            if selection[slotPath] == partId then return true end
            local partItem = Inventory and Inventory.FindPartItem(character, Inventory.ItemIdentifierForPart(part), item) or nil
            if not partItem then
                print("[GunsmithFramework] Missing quick-mod part item for " .. tostring(partId))
                return
            end
            if not QuickMod.CanSlotAcceptItem(item, slotIndex, partItem) then
                print("[GunsmithFramework] Quick-mod XML slot rejects part item for " .. tostring(partId))
                return
            end
            if not QuickMod.ClearSlot(item, character, slotIndex, refreshAfterReturn) then return end
            if not QuickMod.InstallSpecificPartItem(item, character, partItem, slotIndex) then return end
        end

        finishQuickModChange(item, selection, platform, weapon)
        if refreshQuick then
            Runtime.RefreshQuick(item, true)
        else
            Runtime.SchedulePartsRefresh(item, 0, true)
        end
        return false
    end

    local returnedParts = 0
    local refreshWhenReturned = function()
        if not item or item.removed then return end
        if SERVER then
            Persistence.Save(item)
        else
            Runtime.Open(item)
        end
    end

    if partId == Gunsmith.EmptyPartId then
        if Core.IsRequiredSlot(platform, slotPath) then return end
        returnedParts = returnSelectionSubtree(character, item, selection, platform, slotPath, refreshWhenReturned)
        selection[slotPath] = nil
    else
        local part = Gunsmith.Config.parts[partId]
        if not part or not Core.IsPartCompatible(selection, platform, slotPath, partId, ownerId) then return end
        if selection[slotPath] == partId then return end
        if Inventory and not Inventory.ConsumePartItem(character, part, item) then
            print("[GunsmithFramework] Missing part item for " .. tostring(partId))
            return
        end
        returnedParts = returnSelectionSubtree(character, item, selection, platform, slotPath, refreshWhenReturned)
        selection[slotPath] = partId
    end

    Core.ClearDescendants(selection, slotPath)
    Core.PruneInvalidSelections(selection, platform, weapon, ownerId)
    Core.InvalidateQuickSlotsCache(item)
    if Gunsmith.QuickUiSpec then Gunsmith.QuickUiSpec.InvalidateCache(item) end
    saveSelectionIfChanged(item, selection, platform, weapon)
    Runtime.Apply(item)
    if returnedParts and returnedParts > 0 then
        return false
    end
    return true
end

function Runtime.SetPartFromClient(item, character, slotPath, partId)
    if not SERVER or not item or not character or type(slotPath) ~= "string" or type(partId) ~= "string" then return false end
    if QuickMod and QuickMod.SlotForPath(item, slotPath) ~= nil then return false end

    local selection = Runtime.GetSelection(item)
    local platform = Core.PlatformConfig(item)
    if not selection or not platform then return false end

    local before = buildSignature(item, selection, platform, true)
    Runtime.SetPart(item, slotPath, partId, nil, character)
    if before == buildSignature(item, selection, platform, true) then return false end
    return Persistence.Encode(selection, platform, Core.WeaponConfig(item))
end

function Runtime.InstallQuickItem(item, slotPath, draggedItem)
    if SERVER then return false end
    local selection = Runtime.GetSelection(item)
    local platform = Core.PlatformConfig(item)
    local weapon = Core.WeaponConfig(item)
    local ownerId = Core.OwnerForWeapon(weapon)
    if not selection or not platform or not draggedItem then return false end
    if not Core.IsValidPath(selection, platform, slotPath) then return false end
    if not QuickMod then return false end

    local slotIndex = QuickMod.SlotForPath(item, slotPath)
    if slotIndex == nil then return false end

    local partId = QuickMod.PartIdForItem(selection, platform, slotPath, draggedItem, ownerId)
    if not partId then return false end

    local part = Gunsmith.Config.parts[partId]
    if not part or not Core.IsPartCompatible(selection, platform, slotPath, partId, ownerId) then return false end
    if not QuickMod.CanSlotAcceptItem(item, slotIndex, draggedItem) then return false end

    local character = Inventory and Inventory.ActorForItem(item) or nil
    local refreshAfterReturn = function()
        Runtime.RefreshQuick(item)
    end

    if not QuickMod.ClearSlot(item, character, slotIndex, refreshAfterReturn) then return false end
    if not QuickMod.InstallSpecificPartItem(item, character, draggedItem, slotIndex) then
        Runtime.RefreshQuick(item)
        return false
    end

    finishQuickModChange(item, selection, platform, weapon)
    Runtime.RefreshQuick(item, true)
    return true
end

function Runtime.SelectedHandWeapon(character)
    if not character or not character.Inventory then return nil end
    local rightHand = character.Inventory.GetItemInLimbSlot(InvSlotType.RightHand)
    local leftHand = character.Inventory.GetItemInLimbSlot(InvSlotType.LeftHand)
    if Core.WeaponConfig(rightHand) then return rightHand end
    if Core.WeaponConfig(leftHand) then return leftHand end
    return nil
end

function Runtime.FabricatorPartItemIds(item)
    local weapon = Core.WeaponConfig(item)
    local platform = Core.PlatformConfig(item)
    if not weapon or not platform then return "" end

    local ownerId = Core.OwnerForWeapon(weapon)
    local itemIdentifiers = {}
    local visitedParts = {}

    local collectAcceptedParts
    local function collectPart(partId, depth)
        if type(partId) ~= "string" or partId == "" or visitedParts[partId] or depth > 32 then return end
        visitedParts[partId] = true

        local part = Core.GetPart(partId)
        if not part then return end

        if type(part.item) == "table" and type(part.item.identifier) == "string" and part.item.identifier ~= "" then
            itemIdentifiers[part.item.identifier] = true
        end

        if type(part.mounts) ~= "table" then return end
        for _, mount in ipairs(part.mounts) do
            if type(mount) == "table" then
                collectAcceptedParts(mount.partType or mount.path, mount.accepts, depth + 1)
            end
        end
    end

    collectAcceptedParts = function(partType, accepts, depth)
        if type(accepts) ~= "table" or depth > 32 then return end

        if type(partType) == "string" and partType ~= "" then
            for _, partId in ipairs(Core.GetPartsForType(partType, ownerId)) do
                local part = Core.GetPart(partId)
                if part and Core.PartProvidesAccepted(part, accepts) then
                    collectPart(partId, depth)
                end
            end
        end

        for partId, part in pairs(Gunsmith.Config.parts or {}) do
            if Core.CanUsePart(partId, ownerId) and Core.PartProvidesAccepted(part, accepts) then
                collectPart(partId, depth)
            end
        end
    end

    local collectedRoots = {}
    for _, root in ipairs(Core.RootSlotDefs(platform)) do
        local partId = Core.RootPartId(weapon, root.path)
        if type(partId) == "string" then
            collectedRoots[partId] = true
        end
    end
    if type(weapon.roots) == "table" then
        for _, root in pairs(weapon.roots) do
            if type(root) == "table" and type(root.part) == "string" then
                collectedRoots[root.part] = true
            end
        end
    end

    for partId in pairs(collectedRoots) do
        collectPart(partId, 0)
    end

    local result = {}
    for identifier in pairs(itemIdentifiers) do
        table.insert(result, identifier)
    end
    table.sort(result)
    return table.concat(result, ",")
end

function Runtime.Open(item)
    if SERVER then return end
    if not item or not Core.PlatformConfig(item) then return end

    local platform = Core.PlatformConfig(item)
    local selection = Runtime.GetSelection(item)
    if not Hook or not Hook.Call then
        print("[GunsmithFramework] Hook.Call is unavailable; cannot open C# gunsmith UI.")
        return
    end

    if QuickMod and QuickMod.SyncFromContainer(item, selection, platform) then
        saveSelectionIfChanged(item, selection, platform, Core.WeaponConfig(item))
    end

    local currentPath = Runtime.GetCurrentUiPath(item)
    if currentPath ~= "" and #Core.SlotsForPath(selection, platform, currentPath) == 0 then
        currentPath = Core.UiParentPath(platform, currentPath)
        Runtime.SetCurrentUiPath(item, currentPath)
    end

    Runtime.Apply(item)
    Hook.Call("GunsmithFrameworkOpen", item, Core.FrameworkLocalizationKey("ui.title"), UiSpec.Build(item, selection, platform, currentPath))
end

function Runtime.OpenQuick(item)
    if SERVER then return end
    if not item or not Core.PlatformConfig(item) or not QuickMod or not QuickMod.IsQuickItem(item) then return end

    local platform = Core.PlatformConfig(item)
    local selection = Runtime.GetSelection(item)
    if not Hook or not Hook.Call then
        print("[GunsmithFramework] Hook.Call is unavailable; cannot open C# gunsmith quick UI.")
        return
    end

    if QuickMod.SyncFromContainer(item, selection, platform) then
        saveSelectionIfChanged(item, selection, platform, Core.WeaponConfig(item))
    end

    Runtime.Apply(item)
    Hook.Call("GunsmithFrameworkOpenQuick", item, Core.FrameworkLocalizationKey("ui.quick_title"), QuickUiSpec.Build(item, selection, platform))
end

function Runtime.Cleanup(item)
    if Hook and Hook.Call and item then
        Hook.Call("GunsmithFrameworkClearRuntimeState", item)
    end

    local key = Core.ItemKey(item)
    if not key then return end
    State.selections[key] = nil
    State.uiPaths[key] = nil
    State.loadedStates[key] = nil
    State.savedSignatures[item] = nil
    State.pendingPartsRefresh[item] = nil
    State.initialQuickItemsEnsured[item] = nil
    if State.lastQuickSignatures then State.lastQuickSignatures[item] = nil end
    Core.InvalidateQuickSlotsCache(item)
    if Gunsmith.QuickUiSpec then Gunsmith.QuickUiSpec.InvalidateCache(item) end
    invalidateAppliedState(item)
end

Gunsmith.Apply = Runtime.Apply
Gunsmith.Open = Runtime.Open
Gunsmith.OpenQuick = Runtime.OpenQuick
