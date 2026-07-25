CLIENT = true
SERVER = false

local requests = 0
local saves = 0
local quickSyncs = 0
local componentState = ""
local item = {}

GunsmithFramework = {
    Config = {
        parts = {
            saved_handguard = {}
        }
    },
    Core = {
        PlatformConfig = function() return {} end,
        ItemKey = function(value) return value end,
        WeaponConfig = function() return {} end,
        OwnerForWeapon = function() return nil end,
        BuildDefaultSelection = function() return { handguard = "default_handguard" } end,
        RootSlotDefs = function() return {} end,
        SortedSelectionPaths = function() return {} end,
        PruneInvalidSelections = function() end,
        IsRootSlot = function() return false end,
        IsValidPath = function() return true end,
        IsPartCompatible = function() return true end
    },
    QuickMod = {
        SyncFromContainer = function()
            quickSyncs = quickSyncs + 1
            return false
        end
    }
}

Hook = {
    Call = function(name)
        if name == "GunsmithFrameworkRequestState" then requests = requests + 1 end
        if name == "GunsmithFrameworkSaveState" then saves = saves + 1 end
        if name == "GunsmithFrameworkGetSavedState" then return componentState end
    end
}
json = {
    parse = function()
        return { v = 1, parts = { handguard = "saved_handguard" } }
    end
}

dofile("Lua/Scripts/Gunsmith/Persistence.lua")
dofile("Lua/Scripts/Gunsmith/Runtime.lua")

local Runtime = GunsmithFramework.Runtime
assert(Runtime.ResetRoundState == nil)
assert(getmetatable(GunsmithFramework.State.selections) == nil)
assert(getmetatable(GunsmithFramework.State.loadedStates) == nil)
local selection = Runtime.GetSelection(item)
assert(type(selection) == "table")
assert(requests == 1)
Runtime.SyncQuickModContainerItem(item)
assert(requests == 1)
assert(quickSyncs == 1)
GunsmithFramework.Persistence.Save(item)
assert(saves == 0)

CLIENT = false
SERVER = true
Runtime.SyncQuickModContainerItem({})
assert(quickSyncs == 2)
assert(saves == 1)
CLIENT = true
SERVER = false

componentState = "saved-state"
selection = Runtime.GetSelection(item)
assert(selection.handguard == "saved_handguard")
assert(GunsmithFramework.State.loadedStates[item] == true)
GunsmithFramework.Persistence.Save(item)
assert(saves == 2)

local clientNetworkFile = assert(io.open(
    ".AssemblyCSharpSource/GunsmithFramework/ClientProject/ClientSource/GunsmithPartChangeClient.cs",
    "r"))
local clientNetworkSource = clientNetworkFile:read("*a")
clientNetworkFile:close()
assert(clientNetworkSource:find(
    'StateRequestMessageId = "GunsmithFramework.GetPartState.v1"',
    1,
    true))
assert(clientNetworkSource:find(
    "SendToServer(message, DeliveryMethod.Reliable)",
    1,
    true))

local serverNetworkFile = assert(io.open(
    ".AssemblyCSharpSource/GunsmithFramework/ServerProject/ServerSource/GunsmithPartChangeServer.cs",
    "r"))
local serverNetworkSource = serverNetworkFile:read("*a")
serverNetworkFile:close()
assert(serverNetworkSource:find(
    "Receive(StateRequestMessageId, ReceiveStateRequest)",
    1,
    true))
assert(serverNetworkSource:find(
    "SendState(item, state, client.Connection)",
    1,
    true))

local sharedDataFile = assert(io.open(
    ".AssemblyCSharpSource/GunsmithFramework/SharedProject/SharedSource/GunsmithData.cs",
    "r"))
local sharedDataSource = sharedDataFile:read("*a")
sharedDataFile:close()
assert(sharedDataSource:find(
    "GunsmithPartChangeServer.SendState(item, SavedState)",
    1,
    true))
assert(not sharedDataSource:find("item.CreateServerEvent(this)", 1, true))

local clientHooksFile = assert(io.open(
    ".AssemblyCSharpSource/GunsmithFramework/ClientProject/ClientSource/GunsmithHooks.cs",
    "r"))
local clientHooksSource = clientHooksFile:read("*a")
clientHooksFile:close()
assert(clientHooksSource:find(
    "GunsmithPartChangeClient.RequestState(item)",
    1,
    true))
assert(not clientHooksSource:find(
    "GunsmithDataAccess.RequestStateFromServer(item)",
    1,
    true))

local hooksFile = assert(io.open("Lua/Scripts/Gunsmith/Hooks.lua", "r"))
local hooksSource = hooksFile:read("*a")
hooksFile:close()
assert(not hooksSource:find("if not CLIENT then return end", 1, true))
assert(not hooksSource:find(
    'Hook.Patch("Barotrauma.Inventory", "ApplyReceivedState"',
    1,
    true))

print("Server rebroadcasts authoritative quick-slot state after container changes")
