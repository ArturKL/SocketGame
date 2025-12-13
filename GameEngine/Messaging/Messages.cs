using GameEngine.Game;

namespace GameEngine.Messaging;

public enum MessageType : byte
{
    HandshakeRequest = 1,
    HandshakeResponse = 2,

    CreateLobby = 10,
    JoinLobby = 11,
    LobbyCreated = 12,
    PlayerJoined = 13,
    LobbyState = 14,
    StartGame = 15,
    LeaveLobby = 16,

    GameStarted = 30,

    ClaimRankRequest = 40,
    PutDownCardsRequest = 41,
    CallBluffRequest = 42,
    GameStateUpdate = 43,
    CardsPutDownEvent = 44,
    BluffCalledEvent = 45,
    RoundStartedEvent = 46,
    RoundEndedEvent = 47,
    GameEndedEvent = 48,

    Error = 255
}

public class HandshakeRequest
{
    public string PlayerName { get; set; } = string.Empty;
}

public class HandshakeResponse
{
    public string PlayerId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public bool Success { get; set; }
}

public class CreateLobbyRequest
{
}

public class JoinLobbyRequest
{
    public string LobbyCode { get; set; } = string.Empty;
}

public class LeaveLobbyRequest
{
}

public class LobbyCreatedResponse
{
    public string LobbyCode { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
}

public class LobbyStateUpdate
{
    public List<string> PlayerNames { get; set; } = new();
}

public class GameStartedEvent
{
    public List<string> PlayerIds { get; set; } = new();
    public List<string> PlayerNames { get; set; } = new();
}

public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
}

public class ClaimRankRequest
{
    public Rank Rank { get; set; }
}

public class PutDownCardsRequest
{
    public List<int> CardIndices { get; set; } = new();
}

public class CallBluffRequest
{
    public int CardIndexToCheck { get; set; }
}

public class CardInfo
{
    public int Rank { get; set; }
    public int Suit { get; set; }
}

public class GameStateUpdate
{
    public List<CardInfo> OwnHand { get; set; } = new();
    public Dictionary<string, int> PlayerHandCounts { get; set; } = new();
    public int? ClaimedRank { get; set; }
    public Dictionary<string, int> CardsInPlayCounts { get; set; } = new();
    public string CurrentPlayerId { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public int LastBatchCount { get; set; } = 0;
}

public class CardsPutDownEvent
{
    public string PlayerId { get; set; } = string.Empty;
    public int CardCount { get; set; }
}

public class BluffCalledEvent
{
    public string CallerId { get; set; } = string.Empty;
    public int CardIndex { get; set; }
    public CardInfo RevealedCard { get; set; } = new();
    public bool IsBluff { get; set; }
    public string LoserId { get; set; } = string.Empty;
}

public class RoundStartedEvent
{
    public string StartingPlayerId { get; set; } = string.Empty;
    public Rank ClaimedRank { get; set; }
}

public class RoundEndedEvent
{
    public string LoserId { get; set; } = string.Empty;
    public int TotalCardsPickedUp { get; set; }
    public string NextRoundStarterId { get; set; } = string.Empty;
    public List<int> DiscardedRanks { get; set; } = new();
}

public class GameEndedEvent
{
    public string WinnerId { get; set; } = string.Empty;
}