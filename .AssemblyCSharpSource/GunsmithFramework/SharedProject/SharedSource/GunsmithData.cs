using Barotrauma.Networking;

namespace Barotrauma.Items.Components
{
    partial class GunsmithData : ItemComponent, IClientSerializable, IServerSerializable
    {
        private const int MaxSavedStateLength = 8192;

        private enum ClientEventType : byte
        {
            RequestState = 0,
            SaveState = 1
        }

        private readonly struct ClientEventData : IEventData
        {
            public readonly ClientEventType EventType;
            public readonly string SavedState;

            public ClientEventData(ClientEventType eventType, string savedState = "")
            {
                EventType = eventType;
                SavedState = savedState ?? string.Empty;
            }
        }

        private string savedState = string.Empty;

        [Editable, Serialize("", IsPropertySaveable.Yes)]
        public string SavedState
        {
            get => savedState;
            set => savedState = value ?? string.Empty;
        }

        public GunsmithData(Item item, ContentXElement element) : base(item, element)
        {
            IsActive = false;
        }

        public void RequestStateFromServer()
        {
#if CLIENT
            item.CreateClientEvent(this, new ClientEventData(ClientEventType.RequestState));
#endif
        }

        public void SubmitStateToServer(string state)
        {
#if CLIENT
            item.CreateClientEvent(this, new ClientEventData(ClientEventType.SaveState, NormalizeSavedState(state)));
#endif
        }

        public void BroadcastState()
        {
#if SERVER
            item.CreateServerEvent(this);
#endif
        }

        public void ClientEventWrite(IWriteMessage msg, NetEntityEvent.IData? extraData = null)
        {
            if (!TryExtractEventData(extraData, out ClientEventData eventData))
            {
                eventData = new ClientEventData(ClientEventType.RequestState);
            }

            msg.WriteByte((byte)eventData.EventType);
            if (eventData.EventType == ClientEventType.SaveState)
            {
                msg.WriteString(NormalizeSavedState(eventData.SavedState));
            }
        }

        public void ClientEventRead(IReadMessage msg, float sendingTime)
        {
            SavedState = NormalizeSavedState(msg.ReadString());
#if CLIENT
            global::GunsmithFramework.GunsmithLuaHooks.Call("GunsmithFrameworkReceiveState", item, SavedState);
#endif
        }

        public void ServerEventRead(IReadMessage msg, Client c)
        {
            ClientEventType eventType = (ClientEventType)msg.ReadByte();
            switch (eventType)
            {
                case ClientEventType.RequestState:
                    BroadcastState();
                    break;
                case ClientEventType.SaveState:
                    string rawState = msg.ReadString();
                    if (!IsValidSavedState(rawState) || !item.CanClientAccess(c))
                    {
                        BroadcastState();
                        return;
                    }

                    SavedState = NormalizeSavedState(rawState);
#if SERVER
                    global::GunsmithFramework.GunsmithLuaHooks.Call("GunsmithFrameworkReceiveState", item, SavedState);
#endif
                    BroadcastState();
                    break;
            }
        }

        public void ServerEventWrite(IWriteMessage msg, Client c, NetEntityEvent.IData? extraData = null)
        {
            msg.WriteString(NormalizeSavedState(SavedState));
        }

        internal static string NormalizeSavedState(string value)
            => value == null
                ? string.Empty
                : value.Length > MaxSavedStateLength
                    ? value.Substring(0, MaxSavedStateLength)
                    : value;

        internal static bool IsValidSavedState(string value)
        {
            if (value.Length > MaxSavedStateLength)
            {
                return false;
            }

            if (value.Length == 0)
            {
                return true;
            }

            return value.StartsWith("{\"v\":1,\"parts\":{", StringComparison.Ordinal) &&
                   value.EndsWith("}}", StringComparison.Ordinal);
        }
    }
}
