using System.Text.Json;
using Barotrauma.Networking;

namespace Barotrauma.Items.Components
{
    partial class GunsmithData : ItemComponent, IClientSerializable, IServerSerializable
    {
        private const int MaxSavedStateLength = 8192;

        private enum ClientEventType : byte
        {
            RequestState = 0,
            SaveState = 1,
            SetPart = 2
        }

        private readonly struct ClientEventData : IEventData
        {
            public readonly ClientEventType EventType;
            public readonly string SavedState;
            public readonly string SlotPath;
            public readonly string PartId;

            public ClientEventData(ClientEventType eventType, string savedState = "", string slotPath = "", string partId = "")
            {
                EventType = eventType;
                SavedState = savedState ?? string.Empty;
                SlotPath = slotPath ?? string.Empty;
                PartId = partId ?? string.Empty;
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

        public void SubmitPartChangeToServer(string slotPath, string partId)
        {
#if CLIENT
            if (IsValidPartChange(slotPath, partId))
            {
                item.CreateClientEvent(this, new ClientEventData(ClientEventType.SetPart, slotPath: slotPath, partId: partId));
            }
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
            switch (eventData.EventType)
            {
                case ClientEventType.SaveState:
                    msg.WriteString(NormalizeSavedState(eventData.SavedState));
                    break;
                case ClientEventType.SetPart:
                    msg.WriteString(eventData.SlotPath);
                    msg.WriteString(eventData.PartId);
                    break;
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
                case ClientEventType.SetPart:
                    string slotPath = msg.ReadString();
                    string partId = msg.ReadString();
                    bool isOwnedByCharacter = c.Character is { } owner && item.IsOwnedBy(owner);
                    if (!IsValidPartChange(slotPath, partId) ||
                        !CanClientSubmitPartChange(isOwnedByCharacter, item.CanClientAccess(c)))
                    {
                        BroadcastState();
                        return;
                    }

#if SERVER
                    var character = c.Character;
                    if (character != null)
                    {
                        object? result = global::GunsmithFramework.GunsmithLuaHooks.Call("GunsmithFrameworkSetPartFromClient", item, character, slotPath, partId);
                        if (result is string updatedState && IsValidSavedState(updatedState))
                        {
                            SavedState = updatedState;
                        }
                    }
#endif
                    BroadcastState();
                    break;
                default:
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

            try
            {
                using JsonDocument document = JsonDocument.Parse(value);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                bool hasVersion = false;
                bool hasParts = false;
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    switch (property.Name)
                    {
                        case "v":
                            if (hasVersion ||
                                property.Value.ValueKind != JsonValueKind.Number ||
                                !property.Value.TryGetInt32(out int version) ||
                                version != 1)
                            {
                                return false;
                            }
                            hasVersion = true;
                            break;
                        case "parts":
                            if (hasParts || property.Value.ValueKind != JsonValueKind.Object)
                            {
                                return false;
                            }
                            hasParts = true;
                            foreach (JsonProperty part in property.Value.EnumerateObject())
                            {
                                if (string.IsNullOrWhiteSpace(part.Name) ||
                                    part.Value.ValueKind != JsonValueKind.String ||
                                    string.IsNullOrWhiteSpace(part.Value.GetString()))
                                {
                                    return false;
                                }
                            }
                            break;
                        default:
                            return false;
                    }
                }

                return hasVersion && hasParts;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        internal static bool IsValidPartChange(string? slotPath, string? partId)
            => !string.IsNullOrWhiteSpace(slotPath) && !string.IsNullOrWhiteSpace(partId);

        internal static bool CanClientSubmitPartChange(bool isOwnedByCharacter, bool canClientAccess)
            => isOwnedByCharacter || canClientAccess;
    }
}
