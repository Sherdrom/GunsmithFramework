using Barotrauma.Items.Components;
using Barotrauma.Networking;

namespace GunsmithFramework
{
    internal static class GunsmithPartChangeServer
    {
        private const string RequestMessageId = "GunsmithFramework.SetPart.v2";
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
                ushort weaponItemId = message.ReadUInt16();
                ushort workbenchItemId = message.ReadUInt16();
                string slotPath = message.ReadString();
                string partId = message.ReadString();
                if (Entity.FindEntityByID(weaponItemId) is not Item weapon ||
                    weapon.Removed ||
                    client.Character is not { } character ||
                    !GunsmithData.IsValidPartChange(slotPath, partId))
                {
                    return;
                }

                bool hasWorkbench = workbenchItemId != 0;
                Item? workbench = hasWorkbench ? Entity.FindEntityByID(workbenchItemId) as Item : null;
                if (!hasWorkbench)
                {
                    if (!GunsmithData.CanClientSubmitPartChange(weapon.IsOwnedBy(character), weapon.CanClientAccess(client)))
                    {
                        return;
                    }
                }
                else
                {
                    bool workbenchResolved = workbench is { Removed: false };
                    Fabricator? fabricator = workbenchResolved ? workbench!.GetComponent<Fabricator>() : null;
                    if (!CanClientSubmitPartChangeAtWorkbench(
                        weapon.IsOwnedBy(character),
                        workbenchResolved,
                        fabricator != null && GunsmithFabricatorRecipeFilterPatch.IsEnabledFabricator(fabricator),
                        workbenchResolved && workbench!.CanClientAccess(client)))
                    {
                        return;
                    }
                }

                // Lua saves and broadcasts successful changes, including asynchronous returns.
                _ = hasWorkbench
                    ? GunsmithLuaHooks.Call(
                        "GunsmithFrameworkSetPartFromClient",
                        weapon,
                        workbench!,
                        character,
                        slotPath,
                        partId)
                    : GunsmithLuaHooks.Call(
                        "GunsmithFrameworkSetPartFromClient",
                        weapon,
                        character,
                        slotPath,
                        partId);
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

        internal static bool CanClientSubmitPartChangeAtWorkbench(
            bool weaponOwnedByCharacter,
            bool workbenchResolved,
            bool workbenchEnabled,
            bool canClientAccessWorkbench)
            => weaponOwnedByCharacter && workbenchResolved && workbenchEnabled && canClientAccessWorkbench;
    }
}
