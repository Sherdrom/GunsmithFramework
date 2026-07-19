using Barotrauma.Networking;

namespace GunsmithFramework
{
    internal static class GunsmithPartChangeClient
    {
        internal const string RequestMessageId = "GunsmithFramework.SetPart.v1";
        internal const string StateMessageId = "GunsmithFramework.PartState.v1";

        internal static void Register()
            => LuaCsSetup.Instance.Networking.Receive(StateMessageId, ReceiveState);

        internal static bool Submit(Item item, string slotPath, string partId)
        {
            if (GameMain.Client == null ||
                item == null ||
                item.Removed ||
                !Barotrauma.Items.Components.GunsmithData.IsValidPartChange(slotPath, partId))
            {
                return false;
            }

            try
            {
                IWriteMessage message = LuaCsSetup.Instance.Networking.Start(RequestMessageId);
                message.WriteUInt16(item.ID);
                message.WriteString(slotPath);
                message.WriteString(partId);
                LuaCsSetup.Instance.Networking.SendToServer(message, DeliveryMethod.Reliable);
                return true;
            }
            catch (Exception ex)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to submit part change: {ex.Message}");
                return false;
            }
        }

        private static void ReceiveState(IReadMessage message)
        {
            try
            {
                ushort itemId = message.ReadUInt16();
                string state = message.ReadString();
                if (Entity.FindEntityByID(itemId) is not Item item ||
                    item.Removed ||
                    !Barotrauma.Items.Components.GunsmithData.IsValidSavedState(state))
                {
                    return;
                }

                GunsmithDataAccess.SetSavedState(item, state);
                GunsmithLuaHooks.Call("GunsmithFrameworkReceiveState", item, state);
            }
            catch (Exception ex)
            {
                LuaCsSetup.PrintCsMessage($"[GunsmithFramework] Failed to receive part state: {ex.Message}");
            }
        }
    }
}
