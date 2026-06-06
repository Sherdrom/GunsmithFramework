GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = Gunsmith.Core
local Persistence = Gunsmith.Persistence
local Runtime = Gunsmith.Runtime
local QuickMod = Gunsmith.QuickMod
local NpcPresets = Gunsmith.NpcPresets
local Hooks = {}

Gunsmith.Hooks = Hooks

local function readItemAndStrings(args)
    local item = nil
    local strings = {}

    for _, value in ipairs(args) do
        if LuaUserData.IsTargetType(value, "Barotrauma.Item") then
            item = value
        elseif type(value) == "string" then
            table.insert(strings, value)
        end
    end

    return item, strings
end

local function readItemsAndStrings(args)
    local items = {}
    local strings = {}

    for _, value in ipairs(args) do
        if LuaUserData.IsTargetType(value, "Barotrauma.Item") then
            table.insert(items, value)
        elseif type(value) == "string" then
            table.insert(strings, value)
        end
    end

    return items, strings
end

local function applyGunsmithItem(item, diagnoseNpcPreset, npcPresetName)
    if NpcPresets and NpcPresets.TryApply and NpcPresets.TryApply(item, diagnoseNpcPreset, npcPresetName) then
        return
    end
    Runtime.EnsureApplied(item)
end

local function readContainedItem(ptable)
    if not ptable then return nil end

    local item = ptable["containedItem"]
    if item and LuaUserData.IsTargetType(item, "Barotrauma.Item") then
        return item
    end
    return nil
end

local function applyExistingGunsmithItems()
    if not Item or not Item.ItemList then return end

    for item in Item.ItemList do
        applyGunsmithItem(item)
    end
end

local function scheduleExistingItemApply()
    if Timer and Timer.Wait then
        Timer.Wait(applyExistingGunsmithItems, 100)
        Timer.Wait(applyExistingGunsmithItems, 1000)
    else
        applyExistingGunsmithItems()
    end
end

local function addHiddenQuickSlot(slotSet, slotValue)
    local slotIndex = tonumber(slotValue)
    if slotIndex and slotIndex >= 0 and slotIndex % 1 == 0 then
        slotSet[slotIndex] = true
        return slotIndex
    end
    return nil
end

local function hiddenQuickSlotSpec(slotSet)
    local slots = {}
    for slotIndex, _ in pairs(slotSet) do
        table.insert(slots, slotIndex)
    end
    table.sort(slots)
    for index, slotIndex in ipairs(slots) do
        slots[index] = tostring(slotIndex)
    end
    return slots
end

local function stringListSpec(values)
    if type(values) ~= "table" then return "" end

    local result = {}
    local seen = {}
    for _, value in ipairs(values) do
        local text = tostring(value or "")
        if text ~= "" and not seen[text] then
            seen[text] = true
            table.insert(result, text)
        end
    end
    return table.concat(result, ",")
end

local function quickSlotTagSpec(identifier, weapon)
    if type(weapon) == "table" and type(weapon.quickSlotTags) == "table" then
        return stringListSpec(weapon.quickSlotTags)
    end

    local ownerId = Gunsmith.Owners and Gunsmith.Owners.weapons and Gunsmith.Owners.weapons[identifier]
    local package = ownerId and Gunsmith.Packages and Gunsmith.Packages[ownerId] or nil
    return stringListSpec(package and package.partTags or nil)
end

local function registerWeaponTags()
    if not CLIENT or not Hook or not Hook.Call then return end

    local tags = {}
    for _, packageId in ipairs(Gunsmith.PackageOrder or {}) do
        local package = Gunsmith.Packages and Gunsmith.Packages[packageId] or nil
        if type(package) == "table" and type(package.weaponTags) == "table" then
            for _, tag in ipairs(package.weaponTags) do
                table.insert(tags, tag)
            end
        end
    end

    local tagSpec = stringListSpec(tags)
    if tagSpec ~= "" then
        Hook.Call("GunsmithFrameworkRegisterWeaponTags", tagSpec)
    end
end

local function registerHiddenQuickSlots()
    if not Hook or not Hook.Call then return end
    local config = Gunsmith.Config
    if not config or type(config.weapons) ~= "table" then return end

    for identifier, weapon in pairs(config.weapons) do
        local maxSlot = nil
        local hiddenSlots = {}
        local ownerId = Core.OwnerForWeaponId(identifier)
        local platform = Core.PlatformConfigForWeaponId(identifier)
        local selection = platform and Core.BuildDefaultSelection(platform, weapon, ownerId) or nil
        local quickSlots = nil

        if selection and platform and Core.QuickSlotsForSelection then
            quickSlots = Core.QuickSlotsForSelection({ Prefab = { Identifier = { Value = identifier } } }, selection, platform)
        elseif type(weapon.quickSlots) == "table" then
            quickSlots = weapon.quickSlots
        end

        if type(weapon.quickSlotBindings) == "table" then
            for _, binding in pairs(weapon.quickSlotBindings) do
                local slotIndex = type(binding) == "table" and addHiddenQuickSlot(hiddenSlots, binding.slot) or nil
                if slotIndex and (not maxSlot or slotIndex > maxSlot) then
                    maxSlot = slotIndex
                end
            end
        end

        if type(quickSlots) == "table" then
            for _, quickSlot in ipairs(quickSlots) do
                local slotIndex = addHiddenQuickSlot(hiddenSlots, quickSlot.slot)
                if slotIndex and (not maxSlot or slotIndex > maxSlot) then
                    maxSlot = slotIndex
                end
            end

            for _, quickSlot in ipairs(quickSlots) do
                local slotIndex = tonumber(quickSlot.slot)
                if CLIENT and slotIndex and type(quickSlot.showWhenContained) == "table" then
                    local visibleIdentifiers = {}
                    for _, value in ipairs(quickSlot.showWhenContained) do
                        if value and tostring(value) ~= "" then
                            table.insert(visibleIdentifiers, tostring(value))
                        end
                    end

                    if #visibleIdentifiers > 0 then
                        Hook.Call("GunsmithFrameworkRegisterQuickSlotVisibility", tostring(identifier), slotIndex, table.concat(visibleIdentifiers, ","))
                    end
                end
            end
        end

        if CLIENT then
            local slots = hiddenQuickSlotSpec(hiddenSlots)
            if #slots > 0 then
                Hook.Call("GunsmithFrameworkRegisterHiddenQuickSlots", tostring(identifier), table.concat(slots, ","))
            end
        end

        if maxSlot then
            Hook.Call("GunsmithFrameworkRegisterQuickSlotCapacity", tostring(identifier), maxSlot, quickSlotTagSpec(identifier, weapon))
        end
    end
end

function Hooks.RefreshRegistrations()
    registerWeaponTags()
    registerHiddenQuickSlots()
    scheduleExistingItemApply()
end

local function isQuickSlotMutation(item)
    if not Hook or not Hook.Call or not item then return false end
    return Hook.Call("GunsmithFrameworkIsQuickSlotMutation", item) == true
end

local pendingQuickModContainerSync = setmetatable({}, { __mode = "k" })

local function flushQuickModContainerSync(item)
    if not item then return end
    if not pendingQuickModContainerSync[item] then return end
    pendingQuickModContainerSync[item] = nil

    if item.removed or isQuickSlotMutation(item) then return end
    if Core.WeaponConfig(item) and QuickMod and QuickMod.IsQuickItem(item) then
        Runtime.SyncQuickModContainerItem(item)
    end
end

local function syncQuickModContainer(instance)
    if not CLIENT then return end
    if not instance then return end
    local item = instance.Item
    if not item then return end
    if pendingQuickModContainerSync[item] then return end
    if not (Core.WeaponConfig(item) and QuickMod and QuickMod.IsQuickItem(item)) then return end
    if isQuickSlotMutation(item) then return end

    pendingQuickModContainerSync[item] = true
    if Timer and Timer.Wait then
        Timer.Wait(function() flushQuickModContainerSync(item) end, 50)
    else
        flushQuickModContainerSync(item)
    end
end

function Hooks.Register()
    if Hooks.Registered then return end
    Hooks.Registered = true

    registerWeaponTags()
    registerHiddenQuickSlots()

    Hook.Add("GunsmithFrameworkReceiveState", "GunsmithFrameworkReceiveState", function(...)
        local item, strings = readItemAndStrings({ ... })
        if item then
            Persistence.Receive(item, strings[1] or "")
        end
    end)

    Hook.Add("GunsmithFrameworkNpcPresetRegistered", "GunsmithFrameworkNpcPresetRegistered", function(...)
        local item, strings = readItemAndStrings({ ... })
        if item then
            local profileName = strings[1] or ""
            applyGunsmithItem(item, true, profileName)
        end
    end)

    Hook.Patch("Barotrauma.Item", "OnMapLoaded", function(instance, ptable)
        applyGunsmithItem(instance)
    end, Hook.HookMethodType.After)

    Hook.Patch("Barotrauma.Item", ".ctor", { "Microsoft.Xna.Framework.Rectangle", "Barotrauma.ItemPrefab", "Barotrauma.Submarine", "System.Boolean", "System.UInt16" }, function(instance, ptable)
        applyGunsmithItem(instance)
    end, Hook.HookMethodType.After)

    Hook.Patch("Barotrauma.Items.Components.ItemContainer", "OnItemContained", { "Barotrauma.Item", "System.Boolean" }, function(instance, ptable)
        applyGunsmithItem(readContainedItem(ptable))
        syncQuickModContainer(instance)
    end, Hook.HookMethodType.After)

    Hook.Patch("Barotrauma.Items.Components.ItemContainer", "OnItemRemoved", { "Barotrauma.Item" }, function(instance, ptable)
        applyGunsmithItem(readContainedItem(ptable))
        syncQuickModContainer(instance)
    end, Hook.HookMethodType.After)

    Hook.Add("item.removed", "GunsmithFrameworkCleanup", function(item)
        Runtime.Cleanup(item)
    end)

    if not CLIENT then
        scheduleExistingItemApply()
        return
    end

    Hook.Add("GunsmithFrameworkCycle", "GunsmithFrameworkCycle", function(...)
        local item, strings = readItemAndStrings({ ... })
        if item and strings[1] then
            Runtime.CyclePart(item, strings[1])
            Runtime.Open(item)
        end
    end)

    Hook.Add("GunsmithFrameworkSetPart", "GunsmithFrameworkSetPart", function(...)
        local item, strings = readItemAndStrings({ ... })
        if item and strings[1] and strings[2] then
            local shouldOpenNow = Runtime.SetPart(item, strings[1], strings[2])
            if shouldOpenNow ~= false then
                Runtime.Open(item)
            end
        end
    end)

    Hook.Add("GunsmithFrameworkSetQuickPart", "GunsmithFrameworkSetQuickPart", function(...)
        local item, strings = readItemAndStrings({ ... })
        if item and strings[1] and strings[2] then
            local shouldOpenNow = Runtime.SetPart(item, strings[1], strings[2], "quick")
            if shouldOpenNow ~= false then
                Runtime.OpenQuick(item)
            end
        end
    end)

    Hook.Add("GunsmithFrameworkInstallQuickItem", "GunsmithFrameworkInstallQuickItem", function(...)
        local items, strings = readItemsAndStrings({ ... })
        local weaponItem = items[1]
        local draggedItem = items[2]
        if weaponItem and draggedItem and strings[1] then
            Runtime.InstallQuickItem(weaponItem, strings[1], draggedItem)
        end
    end)

    Hook.Add("GunsmithFrameworkSyncQuickContainer", "GunsmithFrameworkSyncQuickContainer", function(...)
        local item = readItemAndStrings({ ... })
        if item then
            Runtime.SyncQuickContainer(item)
        end
    end)

    Hook.Add("GunsmithFrameworkEnterPath", "GunsmithFrameworkEnterPath", function(...)
        local item, strings = readItemAndStrings({ ... })
        if item and strings[1] then
            Runtime.SetCurrentUiPath(item, strings[1])
            Runtime.Open(item)
        end
    end)

    Hook.Patch("Barotrauma.Character", "ControlLocalPlayer", function(instance, ptable)
        if not PlayerInput or not Keys then return end
        if not PlayerInput.KeyHit(Keys.G) then return end

        local shiftDown = false
        shiftDown = PlayerInput.KeyDown(Keys.LeftShift) or PlayerInput.KeyDown(Keys.RightShift)

        local item = Runtime.SelectedHandWeapon(instance)
        if item then
            if shiftDown and QuickMod and QuickMod.IsQuickItem(item) then
                Runtime.OpenQuick(item)
            else
                Runtime.SetCurrentUiPath(item, Runtime.GetCurrentUiPath(item))
                Runtime.Open(item)
            end
        end
    end, Hook.HookMethodType.After)

    scheduleExistingItemApply()
end
