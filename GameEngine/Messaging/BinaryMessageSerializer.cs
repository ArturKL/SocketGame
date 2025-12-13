using System.Text;
using GameEngine.Game;

namespace GameEngine.Messaging;

public class BinaryMessageSerializer : IMessageSerializer
{
    private const byte Version = 1;

    public byte[] Serialize(MessageEnvelope envelope)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Version
        writer.Write(Version);

        // MessageType
        writer.Write((byte)envelope.MessageType);

        // Flags: bit 0 = HasSessionId
        byte flags = 0;
        bool hasSessionId = !string.IsNullOrEmpty(envelope.SessionId);
        if (hasSessionId)
        {
            flags |= 0x01;
        }

        writer.Write(flags);

        // SessionId
        if (hasSessionId && envelope.SessionId != null)
        {
            WriteGuid(writer, envelope.SessionId);
        }

        // Payload
        if (!string.IsNullOrEmpty(envelope.Payload))
        {
            try
            {
                byte[] payloadBytes = Convert.FromBase64String(envelope.Payload);
                writer.Write(payloadBytes);
            }
            catch
            {
                // If it's not base64, something is wrong, write empty payload
            }
        }

        return ms.ToArray();
    }

    public MessageEnvelope? Deserialize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 3)
        {
            return null;
        }

        int offset = 0;

        // Version
        byte version = bytes[offset++];
        if (version != Version)
        {
            return null;
        }

        // MessageType
        MessageType messageType = (MessageType)bytes[offset++];

        // Flags
        byte flags = bytes[offset++];
        bool hasSessionId = (flags & 0x01) != 0;

        // SessionId
        string? sessionId = null;
        if (hasSessionId)
        {
            if (bytes.Length < offset + 16)
            {
                return null;
            }

            sessionId = ReadGuid(bytes.Slice(offset, 16));
            offset += 16;
        }

        // Payload
        var payloadBytes = bytes.Slice(offset);
        string payload = Convert.ToBase64String(payloadBytes);

        return new MessageEnvelope
        {
            MessageType = messageType,
            SessionId = sessionId,
            Payload = payload
        };
    }

    public string SerializePayload<T>(T payload)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        SerializePayload(writer, GetMessageTypeForPayload<T>(), payload);
        return Convert.ToBase64String(ms.ToArray());
    }

    public T? DeserializePayload<T>(string payload)
    {
        var bytes = Convert.FromBase64String(payload);
        return DeserializePayload<T>(GetMessageTypeForPayload<T>(), bytes);
    }

    private void SerializePayload<T>(BinaryWriter writer, MessageType messageType, T payload)
    {
        switch (messageType)
        {
            case MessageType.HandshakeRequest:
                if (payload is HandshakeRequest hr)
                {
                    WriteHandshakeRequest(writer, hr);
                }
                break;
            case MessageType.HandshakeResponse:
                if (payload is HandshakeResponse hresp)
                {
                    WriteHandshakeResponse(writer, hresp);
                }
                break;
            case MessageType.ClaimRankRequest:
                if (payload is ClaimRankRequest crr)
                {
                    WriteClaimRankRequest(writer, crr);
                }
                break;
            case MessageType.PutDownCardsRequest:
                if (payload is PutDownCardsRequest pdcr)
                {
                    WritePutDownCardsRequest(writer, pdcr);
                }
                break;
            case MessageType.CallBluffRequest:
                if (payload is CallBluffRequest cbr)
                {
                    WriteCallBluffRequest(writer, cbr);
                }
                break;
            case MessageType.GameStarted:
                if (payload is GameStartedEvent gse)
                {
                    WriteGameStartedEvent(writer, gse);
                }
                break;
            case MessageType.GameStateUpdate:
                if (payload is GameStateUpdate gsu)
                {
                    WriteGameStateUpdate(writer, gsu);
                }
                break;
            case MessageType.CardsPutDownEvent:
                if (payload is CardsPutDownEvent cpde)
                {
                    WriteCardsPutDownEvent(writer, cpde);
                }
                break;
            case MessageType.BluffCalledEvent:
                if (payload is BluffCalledEvent bce)
                {
                    WriteBluffCalledEvent(writer, bce);
                }
                break;
            case MessageType.RoundStartedEvent:
                if (payload is RoundStartedEvent rse)
                {
                    WriteRoundStartedEvent(writer, rse);
                }
                break;
            case MessageType.RoundEndedEvent:
                if (payload is RoundEndedEvent ree)
                {
                    WriteRoundEndedEvent(writer, ree);
                }
                break;
            case MessageType.GameEndedEvent:
                if (payload is GameEndedEvent gee)
                {
                    WriteGameEndedEvent(writer, gee);
                }
                break;
            case MessageType.LobbyCreated:
                if (payload is LobbyCreatedResponse lcr)
                {
                    WriteLobbyCreatedResponse(writer, lcr);
                }
                break;
            case MessageType.LobbyState:
                if (payload is LobbyStateUpdate lsu)
                {
                    WriteLobbyStateUpdate(writer, lsu);
                }
                break;
            case MessageType.Error:
                if (payload is ErrorResponse err)
                {
                    WriteErrorResponse(writer, err);
                }
                break;
            case MessageType.JoinLobby:
                if (payload is JoinLobbyRequest jlr)
                {
                    WriteJoinLobbyRequest(writer, jlr);
                }
                break;
        }
    }

    private T? DeserializePayload<T>(MessageType messageType, ReadOnlySpan<byte> bytes)
    {
        using var ms = new MemoryStream(bytes.ToArray());
        using var reader = new BinaryReader(ms);

        return messageType switch
        {
            MessageType.HandshakeRequest => (T)(object)ReadHandshakeRequest(reader),
            MessageType.HandshakeResponse => (T)(object)ReadHandshakeResponse(reader),
            MessageType.ClaimRankRequest => (T)(object)ReadClaimRankRequest(reader),
            MessageType.PutDownCardsRequest => (T)(object)ReadPutDownCardsRequest(reader),
            MessageType.CallBluffRequest => (T)(object)ReadCallBluffRequest(reader),
            MessageType.GameStarted => (T)(object)ReadGameStartedEvent(reader),
            MessageType.GameStateUpdate => (T)(object)ReadGameStateUpdate(reader),
            MessageType.CardsPutDownEvent => (T)(object)ReadCardsPutDownEvent(reader),
            MessageType.BluffCalledEvent => (T)(object)ReadBluffCalledEvent(reader),
            MessageType.RoundStartedEvent => (T)(object)ReadRoundStartedEvent(reader),
            MessageType.RoundEndedEvent => (T)(object)ReadRoundEndedEvent(reader),
            MessageType.GameEndedEvent => (T)(object)ReadGameEndedEvent(reader),
            MessageType.LobbyCreated => (T)(object)ReadLobbyCreatedResponse(reader),
            MessageType.LobbyState => (T)(object)ReadLobbyStateUpdate(reader),
            MessageType.Error => (T)(object)ReadErrorResponse(reader),
            MessageType.JoinLobby => (T)(object)ReadJoinLobbyRequest(reader),
            _ => default
        };
    }

    private void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > 255)
        {
            writer.Write((ushort)bytes.Length);
        }
        else
        {
            writer.Write((byte)bytes.Length);
        }

        writer.Write(bytes);
    }

    private string ReadString(BinaryReader reader)
    {
        int length = reader.ReadByte();
        if (length == 255)
        {
            length = reader.ReadUInt16();
        }

        var bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private void WriteGuid(BinaryWriter writer, string guidString)
    {
        if (string.IsNullOrEmpty(guidString))
        {
            writer.Write(new byte[16]
            {
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
            });
            return;
        }

        if (Guid.TryParse(guidString, out var guid))
        {
            writer.Write(guid.ToByteArray());
        }
        else
        {
            writer.Write(new byte[16]); // Write zeros if invalid
        }
    }

    private string ReadGuid(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16)
        {
            return Guid.Empty.ToString();
        }

        bool isEmpty = true;
        for (int i = 0; i < 16; i++)
        {
            if (bytes[i] != 0xFF)
            {
                isEmpty = false;
                break;
            }
        }

        if (isEmpty)
        {
            return string.Empty;
        }

        return new Guid(bytes.Slice(0, 16)).ToString();
    }

    private void WriteCardInfo(BinaryWriter writer, CardInfo card)
    {
        writer.Write((byte)card.Rank);
        writer.Write((byte)card.Suit);
    }

    private CardInfo ReadCardInfo(BinaryReader reader)
    {
        return new CardInfo
        {
            Rank = reader.ReadByte(),
            Suit = reader.ReadByte()
        };
    }

    // Message-specific serialization methods
    private void WriteHandshakeRequest(BinaryWriter writer, HandshakeRequest req)
    {
        WriteString(writer, req.PlayerName);
    }

    private HandshakeRequest ReadHandshakeRequest(BinaryReader reader)
    {
        return new HandshakeRequest { PlayerName = ReadString(reader) };
    }

    private void WriteHandshakeResponse(BinaryWriter writer, HandshakeResponse resp)
    {
        writer.Write(resp.Success ? (byte)1 : (byte)0);
        WriteGuid(writer, resp.PlayerId);
        WriteGuid(writer, resp.SessionId);
    }

    private HandshakeResponse ReadHandshakeResponse(BinaryReader reader)
    {
        return new HandshakeResponse
        {
            Success = reader.ReadByte() != 0,
            PlayerId = ReadGuid(reader.ReadBytes(16)),
            SessionId = ReadGuid(reader.ReadBytes(16))
        };
    }

    private void WriteClaimRankRequest(BinaryWriter writer, ClaimRankRequest req)
    {
        writer.Write((byte)req.Rank);
    }

    private ClaimRankRequest ReadClaimRankRequest(BinaryReader reader)
    {
        return new ClaimRankRequest { Rank = (Rank)reader.ReadByte() };
    }

    private void WritePutDownCardsRequest(BinaryWriter writer, PutDownCardsRequest req)
    {
        writer.Write((byte)req.CardIndices.Count);
        foreach (var idx in req.CardIndices)
        {
            writer.Write((byte)idx);
        }
    }

    private PutDownCardsRequest ReadPutDownCardsRequest(BinaryReader reader)
    {
        var count = reader.ReadByte();
        var indices = new List<int>();
        for (int i = 0; i < count; i++)
        {
            indices.Add(reader.ReadByte());
        }

        return new PutDownCardsRequest { CardIndices = indices };
    }

    private void WriteCallBluffRequest(BinaryWriter writer, CallBluffRequest req)
    {
        writer.Write((byte)req.CardIndexToCheck);
    }

    private CallBluffRequest ReadCallBluffRequest(BinaryReader reader)
    {
        return new CallBluffRequest { CardIndexToCheck = reader.ReadByte() };
    }

    private void WriteGameStartedEvent(BinaryWriter writer, GameStartedEvent evt)
    {
        writer.Write((byte)evt.PlayerIds.Count);
        for (int i = 0; i < evt.PlayerIds.Count; i++)
        {
            WriteGuid(writer, evt.PlayerIds[i]);
            WriteString(writer, evt.PlayerNames[i]);
        }
    }

    private GameStartedEvent ReadGameStartedEvent(BinaryReader reader)
    {
        var count = reader.ReadByte();
        var playerIds = new List<string>();
        var playerNames = new List<string>();
        for (int i = 0; i < count; i++)
        {
            playerIds.Add(ReadGuid(reader.ReadBytes(16)));
            playerNames.Add(ReadString(reader));
        }

        return new GameStartedEvent { PlayerIds = playerIds, PlayerNames = playerNames };
    }

    private void WriteGameStateUpdate(BinaryWriter writer, GameStateUpdate state)
    {
        // OwnHand
        writer.Write((byte)state.OwnHand.Count);
        foreach (var card in state.OwnHand)
        {
            WriteCardInfo(writer, card);
        }

        // PlayerHandCounts
        writer.Write((byte)state.PlayerHandCounts.Count);
        foreach (var kvp in state.PlayerHandCounts)
        {
            WriteGuid(writer, kvp.Key);
            writer.Write((byte)kvp.Value);
        }

        // ClaimedRank
        writer.Write(state.ClaimedRank.HasValue ? (byte)1 : (byte)0);
        if (state.ClaimedRank.HasValue)
        {
            writer.Write((byte)state.ClaimedRank.Value);
        }

        // CardsInPlayCounts
        writer.Write((byte)state.CardsInPlayCounts.Count);
        foreach (var kvp in state.CardsInPlayCounts)
        {
            WriteGuid(writer, kvp.Key);
            writer.Write((byte)kvp.Value);
        }

        // CurrentPlayerId
        WriteGuid(writer, state.CurrentPlayerId);

        // Phase
        byte phase = state.Phase switch
        {
            "WaitingForClaim" => 0,
            "Playing" => 1,
            "CheckingBluff" => 2,
            _ => 0
        };
        writer.Write(phase);

        // LastBatchCount
        writer.Write((byte)state.LastBatchCount);
    }

    private GameStateUpdate ReadGameStateUpdate(BinaryReader reader)
    {
        var state = new GameStateUpdate();

        // OwnHand
        var handCount = reader.ReadByte();
        state.OwnHand = new List<CardInfo>();
        for (int i = 0; i < handCount; i++)
        {
            state.OwnHand.Add(ReadCardInfo(reader));
        }

        // PlayerHandCounts
        var countsCount = reader.ReadByte();
        state.PlayerHandCounts = new Dictionary<string, int>();
        for (int i = 0; i < countsCount; i++)
        {
            var playerId = ReadGuid(reader.ReadBytes(16));
            var count = reader.ReadByte();
            state.PlayerHandCounts[playerId] = count;
        }

        // ClaimedRank
        bool hasRank = reader.ReadByte() != 0;
        if (hasRank)
        {
            state.ClaimedRank = reader.ReadByte();
        }

        // CardsInPlayCounts
        var playCountsCount = reader.ReadByte();
        state.CardsInPlayCounts = new Dictionary<string, int>();
        for (int i = 0; i < playCountsCount; i++)
        {
            var playerId = ReadGuid(reader.ReadBytes(16));
            var count = reader.ReadByte();
            state.CardsInPlayCounts[playerId] = count;
        }

        // CurrentPlayerId
        state.CurrentPlayerId = ReadGuid(reader.ReadBytes(16));

        // Phase
        byte phase = reader.ReadByte();
        state.Phase = phase switch
        {
            0 => "WaitingForClaim",
            1 => "Playing",
            2 => "CheckingBluff",
            _ => "WaitingForClaim"
        };

        // LastBatchCount
        state.LastBatchCount = reader.ReadByte();

        return state;
    }

    private void WriteCardsPutDownEvent(BinaryWriter writer, CardsPutDownEvent evt)
    {
        WriteGuid(writer, evt.PlayerId);
        writer.Write((byte)evt.CardCount);
    }

    private CardsPutDownEvent ReadCardsPutDownEvent(BinaryReader reader)
    {
        return new CardsPutDownEvent
        {
            PlayerId = ReadGuid(reader.ReadBytes(16)),
            CardCount = reader.ReadByte()
        };
    }

    private void WriteBluffCalledEvent(BinaryWriter writer, BluffCalledEvent evt)
    {
        WriteGuid(writer, evt.CallerId);
        writer.Write((byte)evt.CardIndex);
        WriteCardInfo(writer, evt.RevealedCard);
        writer.Write(evt.IsBluff ? (byte)1 : (byte)0);
        WriteGuid(writer, evt.LoserId);
    }

    private BluffCalledEvent ReadBluffCalledEvent(BinaryReader reader)
    {
        return new BluffCalledEvent
        {
            CallerId = ReadGuid(reader.ReadBytes(16)),
            CardIndex = reader.ReadByte(),
            RevealedCard = ReadCardInfo(reader),
            IsBluff = reader.ReadByte() != 0,
            LoserId = ReadGuid(reader.ReadBytes(16))
        };
    }

    private void WriteRoundStartedEvent(BinaryWriter writer, RoundStartedEvent evt)
    {
        WriteGuid(writer, evt.StartingPlayerId);
        writer.Write((byte)evt.ClaimedRank);
    }

    private RoundStartedEvent ReadRoundStartedEvent(BinaryReader reader)
    {
        return new RoundStartedEvent
        {
            StartingPlayerId = ReadGuid(reader.ReadBytes(16)),
            ClaimedRank = (Rank)reader.ReadByte()
        };
    }

    private void WriteRoundEndedEvent(BinaryWriter writer, RoundEndedEvent evt)
    {
        WriteGuid(writer, evt.LoserId);
        writer.Write((byte)evt.TotalCardsPickedUp);
        WriteGuid(writer, evt.NextRoundStarterId);
        writer.Write((byte)evt.DiscardedRanks.Count);
        foreach (var rank in evt.DiscardedRanks)
        {
            writer.Write((byte)rank);
        }
    }

    private RoundEndedEvent ReadRoundEndedEvent(BinaryReader reader)
    {
        var evt = new RoundEndedEvent
        {
            LoserId = ReadGuid(reader.ReadBytes(16)),
            TotalCardsPickedUp = reader.ReadByte(),
            NextRoundStarterId = ReadGuid(reader.ReadBytes(16))
        };

        var ranksCount = reader.ReadByte();
        evt.DiscardedRanks = new List<int>();
        for (int i = 0; i < ranksCount; i++)
        {
            evt.DiscardedRanks.Add(reader.ReadByte());
        }

        return evt;
    }

    private void WriteGameEndedEvent(BinaryWriter writer, GameEndedEvent evt)
    {
        WriteGuid(writer, evt.WinnerId);
    }

    private GameEndedEvent ReadGameEndedEvent(BinaryReader reader)
    {
        return new GameEndedEvent
        {
            WinnerId = ReadGuid(reader.ReadBytes(16))
        };
    }

    private void WriteLobbyCreatedResponse(BinaryWriter writer, LobbyCreatedResponse resp)
    {
        WriteString(writer, resp.LobbyCode);
        WriteGuid(writer, resp.OwnerId);
    }

    private LobbyCreatedResponse ReadLobbyCreatedResponse(BinaryReader reader)
    {
        return new LobbyCreatedResponse
        {
            LobbyCode = ReadString(reader),
            OwnerId = ReadGuid(reader.ReadBytes(16))
        };
    }

    private void WriteLobbyStateUpdate(BinaryWriter writer, LobbyStateUpdate update)
    {
        writer.Write((byte)update.PlayerNames.Count);
        foreach (var name in update.PlayerNames)
        {
            WriteString(writer, name);
        }
    }

    private LobbyStateUpdate ReadLobbyStateUpdate(BinaryReader reader)
    {
        var count = reader.ReadByte();
        var names = new List<string>();
        for (int i = 0; i < count; i++)
        {
            names.Add(ReadString(reader));
        }

        return new LobbyStateUpdate { PlayerNames = names };
    }

    private void WriteErrorResponse(BinaryWriter writer, ErrorResponse err)
    {
        WriteString(writer, err.Message);
    }

    private ErrorResponse ReadErrorResponse(BinaryReader reader)
    {
        return new ErrorResponse { Message = ReadString(reader) };
    }

    private void WriteJoinLobbyRequest(BinaryWriter writer, JoinLobbyRequest req)
    {
        WriteString(writer, req.LobbyCode);
    }

    private JoinLobbyRequest ReadJoinLobbyRequest(BinaryReader reader)
    {
        return new JoinLobbyRequest { LobbyCode = ReadString(reader) };
    }

    private MessageType GetMessageTypeForPayload<T>()
    {
        return typeof(T).Name switch
        {
            nameof(HandshakeRequest) => MessageType.HandshakeRequest,
            nameof(HandshakeResponse) => MessageType.HandshakeResponse,
            nameof(ClaimRankRequest) => MessageType.ClaimRankRequest,
            nameof(PutDownCardsRequest) => MessageType.PutDownCardsRequest,
            nameof(CallBluffRequest) => MessageType.CallBluffRequest,
            nameof(GameStartedEvent) => MessageType.GameStarted,
            nameof(GameStateUpdate) => MessageType.GameStateUpdate,
            nameof(CardsPutDownEvent) => MessageType.CardsPutDownEvent,
            nameof(BluffCalledEvent) => MessageType.BluffCalledEvent,
            nameof(RoundStartedEvent) => MessageType.RoundStartedEvent,
            nameof(RoundEndedEvent) => MessageType.RoundEndedEvent,
            nameof(GameEndedEvent) => MessageType.GameEndedEvent,
            nameof(LobbyCreatedResponse) => MessageType.LobbyCreated,
            nameof(LobbyStateUpdate) => MessageType.LobbyState,
            nameof(ErrorResponse) => MessageType.Error,
            nameof(JoinLobbyRequest) => MessageType.JoinLobby,
            _ => MessageType.Error
        };
    }
}