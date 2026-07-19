GunsmithFramework = {
    Config = { parts = {} },
    EmptyPartId = "__empty"
}

dofile("Lua/Scripts/Gunsmith/Core.lua")

local serialized = {}
local quoted = {
    ["receiver"] = "\"receiver\"",
    ["stock"] = "\"stock\"",
    ["receiver/barrel"] = "\"receiver\\/barrel\"",
    ["receiver/barrel/muzzle"] = "\"receiver\\/barrel\\/muzzle\"",
    ["receiver/handguard"] = "\"receiver\\/handguard\"",
    ["receiver/optic"] = "\"receiver\\/optic\"",
    ["receiver-id"] = "\"receiver-id\"",
    ["barrel-id"] = "\"barrel-id\"",
    ["muzzle-id"] = "\"muzzle-id\"",
    ["optic-id"] = "\"optic-id\"",
    ["__empty"] = "\"__empty\""
}
local version1Fixture = "{\"v\":1,\"parts\":{\"receiver\":\"receiver-id\",\"receiver/barrel\":\"barrel-id\"}}"
local parsedParts = {
    receiver = "receiver-id",
    ["receiver/barrel"] = "barrel-id",
    [42] = "not-a-string-path",
    emptyPath = "",
    nonStringValue = 42,
    booleanValue = false,
    [""] = "empty-path"
}
local parsedValues = {
    [version1Fixture] = { v = 1, parts = parsedParts },
    ["empty-parts"] = { v = 1, parts = {} },
    ["wrong-version"] = { v = 2, parts = {} },
    ["missing-parts"] = { v = 1 },
    ["non-table-parts"] = { v = 1, parts = "parts" }
}
local parseCalls = {}

json = {
    serialize = function(value)
        assert(type(value) == "table" and value[2] == nil)
        local stringValue = value[1]
        assert(type(stringValue) == "string")
        table.insert(serialized, stringValue)
        assert(quoted[stringValue] ~= nil)
        return "[" .. quoted[stringValue] .. "]"
    end,
    parse = function(value)
        table.insert(parseCalls, value)
        if value == "malformed" then error("malformed JSON") end
        return parsedValues[value]
    end
}

dofile("Lua/Scripts/Gunsmith/Persistence.lua")

local Persistence = GunsmithFramework.Persistence
local platform = {
    rootSlots = {
        { path = "receiver" },
        { path = "stock" }
    }
}

local originalBuildDefaultSelection = GunsmithFramework.Core.BuildDefaultSelection
local originalIsRequiredSlot = GunsmithFramework.Core.IsRequiredSlot
local originalIsValidPath = GunsmithFramework.Core.IsValidPath
GunsmithFramework.Core.BuildDefaultSelection = function()
    return { ["receiver/handguard"] = "default-handguard" }
end
GunsmithFramework.Core.IsRequiredSlot = function() return false end
GunsmithFramework.Core.IsValidPath = function(_, _, path)
    return path == "receiver/handguard"
end

local encoded = Persistence.Encode({
    receiver = "receiver-id",
    ["receiver/barrel"] = "barrel-id",
    ["receiver/barrel/muzzle"] = "muzzle-id",
    ["receiver/optic"] = "optic-id"
}, platform)

GunsmithFramework.Core.BuildDefaultSelection = originalBuildDefaultSelection
GunsmithFramework.Core.IsRequiredSlot = originalIsRequiredSlot
GunsmithFramework.Core.IsValidPath = originalIsValidPath

assert(encoded == "{\"v\":1,\"parts\":{\"receiver\":\"receiver-id\",\"stock\":\"__empty\",\"receiver\\/barrel\":\"barrel-id\",\"receiver\\/barrel\\/muzzle\":\"muzzle-id\",\"receiver\\/handguard\":\"__empty\",\"receiver\\/optic\":\"optic-id\"}}")

local expectedSerialized = {
    "receiver", "receiver-id",
    "stock", "__empty",
    "receiver/barrel", "barrel-id",
    "receiver/barrel/muzzle", "muzzle-id",
    "receiver/handguard", "__empty",
    "receiver/optic", "optic-id"
}
assert(#serialized == #expectedSerialized)
for index, value in ipairs(expectedSerialized) do
    assert(serialized[index] == value)
end

assert(Persistence.Decode("") == nil)
assert(#parseCalls == 0)

local decoded = Persistence.Decode(version1Fixture)
assert(decoded ~= parsedParts)
assert(decoded.receiver == "receiver-id")
assert(decoded["receiver/barrel"] == "barrel-id")
assert(decoded[42] == nil)
assert(decoded.emptyPath == nil)
assert(decoded.nonStringValue == nil)
assert(decoded.booleanValue == nil)
assert(decoded[""] == nil)

local emptyParts = Persistence.Decode("empty-parts")
assert(type(emptyParts) == "table" and next(emptyParts) == nil)
assert(Persistence.Decode("malformed") == nil)
assert(Persistence.Decode("wrong-version") == nil)
assert(Persistence.Decode("missing-parts") == nil)
assert(Persistence.Decode("non-table-parts") == nil)

local refreshed = 0
local scheduled
local item = {}
GunsmithFramework.State = {
    selections = {},
    loadedStates = {},
    appliedSignatures = {},
    appliedConfigSignatures = {},
    lastQuickSignatures = {}
}
GunsmithFramework.Core.PlatformConfig = function() return platform end
GunsmithFramework.Core.WeaponConfig = function() return {} end
GunsmithFramework.Core.ItemKey = function() return "weapon" end
GunsmithFramework.Core.BuildDefaultSelection = function() return {} end
GunsmithFramework.Core.PruneInvalidSelections = function() end
GunsmithFramework.Runtime = {
    Apply = function() end,
    RefreshParts = function() refreshed = refreshed + 1 end,
    SchedulePartsRefresh = function(receivedItem, delay, alreadySynced)
        scheduled = { item = receivedItem, delay = delay, alreadySynced = alreadySynced }
    end
}
Persistence.Receive(item, "empty-parts")
assert(refreshed == 1)
assert(scheduled.item == item and scheduled.delay == 100 and scheduled.alreadySynced == true)

print("Persistence JSON uses native serialization and validates decoded version 1 state")
