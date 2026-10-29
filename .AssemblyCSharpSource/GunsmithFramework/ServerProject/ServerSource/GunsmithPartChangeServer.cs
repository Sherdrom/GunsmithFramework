using Barotrauma.Items.Components;
using Barotrauma.Networking;
using System.Runtime.CompilerServices;

namespace GunsmithFramework
{
    internal static class GunsmithPartChangeServer
    {
        private const string RequestMessageId = "GunsmithFramework.SetPart.v2";
        private const string StateRequestMessageId = "GunsmithFramework.GetPartState.v1";
        private const string StateMessageId = "GunsmithFramework.PartState.v1";
        private sealed class LegacyRequestState { internal double NextAllowed; }
        private static readonly ConditionalWeakTable<Client, LegacyRequestState> LegacyRequests = new();

        internal static void Register()
        {
            LuaCsSetup.Instance.Networking.Receive(RequestMessageId, ReceivePartChange);
            LuaCsSetup.Instance.Networking.Receive(StateRequestMessageId, ReceiveStateRequest);
        }

        private static void ReceiveStateRequest(IReadMessage message, Client client)
        {
            try
            {
                ushort itemId = message.ReadUInt16();
                if (Entity.FindEntityByID(itemId) is not Item item || item.Removed)
                {
                    return;
                }

                string state = GunsmithDataAccess.GetSavedState(item);
                if (!GunsmithData.IsValidSavedState(state))
                {
                    return;
                }

                SendState(item, state, client.Connection);
            }
            catch (Exception ex)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to handle part state request: {ex.Message}");
            }
        }

        private static void ReceivePartChange(IReadMessage message, Client client)
        {
            try
            {
                ushort weaponItemId = message.ReadUInt16();
                ushort workbenchItemId = message.ReadUInt16();
                string slotPath = message.ReadString();
                string partId = message.ReadString();
                if (Entity.FindEntityByID(weaponItemId) is Item weapon)
                {
                    TryApplyPartChange(weapon, client, workbenchItemId, slotPath, partId);
                }
            }
            catch (Exception ex)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to handle part change: {ex.Message}");
            }
        }

        private static bool TryApplyPartChange(Item weapon, Client client, ushort workbenchItemId, string slotPath, string partId)
        {
            if (weapon.Removed || client.Character is not { } character || !GunsmithData.IsValidPartChange(slotPath, partId))
            {
                return false;
            }
            bool hasWorkbench = workbenchItemId != 0;
            Item? workbench = hasWorkbench ? Entity.FindEntityByID(workbenchItemId) as Item : null;
            if (!hasWorkbench)
            {
                if (!GunsmithData.CanClientSubmitPartChange(weapon.IsOwnedBy(character), weapon.CanClientAccess(client)))
                {
                    return false;
                }
            }
            else
            {
                bool workbenchResolved = workbench is { Removed: false };
                Fabricator? fabricator = workbenchResolved ? workbench!.GetComponent<Fabricator>() : null;
                if (!CanClientSubmitPartChangeAtWorkbench(
                    weapon.IsOwnedBy(character), workbenchResolved,
                    fabricator != null && GunsmithFabricatorRecipeFilterPatch.IsEnabledFabricator(fabricator),
                    workbenchResolved && workbench!.CanClientAccess(client)))
                {
                    return false;
                }
            }

            // Lua saves and broadcasts successful changes, including asynchronous returns.
            object? result = hasWorkbench
                ? GunsmithLuaHooks.Call("GunsmithFrameworkSetPartFromClient", weapon, workbench!, character, slotPath, partId)
                : GunsmithLuaHooks.Call("GunsmithFrameworkSetPartFromClient", weapon, character, slotPath, partId);
            return result is string state && GunsmithData.IsValidSavedState(state);
        }

        internal static bool TryAcceptLegacyRequest(ref double nextAllowed, double now)
        {
            if (now < nextAllowed) { return false; }
            nextAllowed = now + 0.25;
            return true;
        }

        internal static void ReceiveLegacyRequest(Item item, Client client, string? slotPath, string? partId)
        {
            if (item == null || item.Removed || client?.Connection == null) { return; }
            LegacyRequestState gate = LegacyRequests.GetValue(client, _ => new LegacyRequestState());
            if (!TryAcceptLegacyRequest(ref gate.NextAllowed, Timing.TotalTime)) { return; }
            try
            {
                if (slotPath != null && partId != null && TryApplyPartChange(item, client, 0, slotPath, partId)) { return; }
                SendState(item, GunsmithDataAccess.GetSavedState(item), client.Connection);
            }
            catch (Exception ex)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to handle legacy request: {ex.Message}");
            }
        }

        internal static void SendState(Item item, string state, NetworkConnection? connection = null)
        {
            if (!GunsmithData.IsValidSavedState(state)) { return; }
            IWriteMessage response = LuaCsSetup.Instance.Networking.Start(StateMessageId);
            response.WriteUInt16(item.ID);
            response.WriteString(state);
            LuaCsSetup.Instance.Networking.SendToClient(response, connection, DeliveryMethod.Reliable);
        }

        internal static bool CanClientSubmitPartChangeAtWorkbench(
            bool weaponOwnedByCharacter,
            bool workbenchResolved,
            bool workbenchEnabled,
            bool canClientAccessWorkbench)
            => weaponOwnedByCharacter && workbenchResolved && workbenchEnabled && canClientAccessWorkbench;
    }
}
