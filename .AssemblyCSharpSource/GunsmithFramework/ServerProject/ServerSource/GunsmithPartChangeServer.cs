using Barotrauma.Items.Components;
using Barotrauma.Networking;

namespace GunsmithFramework
{
    internal static class GunsmithPartChangeServer
    {
        private const string RequestMessageId = "GunsmithFramework.SetPart.v1";
        private const string StateRequestMessageId = "GunsmithFramework.GetPartState.v1";
        private const string StateMessageId = "GunsmithFramework.PartState.v1";

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
                ushort itemId = message.ReadUInt16();
                string slotPath = message.ReadString();
                string partId = message.ReadString();
                if (Entity.FindEntityByID(itemId) is not Item item ||
                    item.Removed ||
                    client.Character is not { } character ||
                    !GunsmithData.IsValidPartChange(slotPath, partId) ||
                    !GunsmithData.CanClientSubmitPartChange(item.IsOwnedBy(character), item.CanClientAccess(client)))
                {
                    return;
                }

                object? result = GunsmithLuaHooks.Call(
                    "GunsmithFrameworkSetPartFromClient",
                    item,
                    character,
                    slotPath,
                    partId);
                if (result is not string state || !GunsmithData.IsValidSavedState(state))
                {
                    return;
                }

                GunsmithDataAccess.SetSavedState(item, state);
                SendState(item, state);
            }
            catch (Exception ex)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to handle part change: {ex.Message}");
            }
        }

        internal static void SendState(Item item, string state, NetworkConnection? connection = null)
        {
            IWriteMessage response = LuaCsSetup.Instance.Networking.Start(StateMessageId);
            response.WriteUInt16(item.ID);
            response.WriteString(state);
            LuaCsSetup.Instance.Networking.SendToClient(response, connection, DeliveryMethod.Reliable);
        }
    }
}
