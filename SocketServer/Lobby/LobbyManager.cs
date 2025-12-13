using System.Collections.Concurrent;
using GameEngine.Game;
using GameEngine.Messaging;

namespace SocketServer.Lobby;

public class GameRoom
{
    public string LobbyCode { get; }
    public string OwnerId { get; private set; }
    private readonly List<Session> _players = new();
    private readonly CheatGameState _gameState = new();
    private readonly Deck _deck = new();
    private readonly object _gameLock = new();

    public int PlayerCount => _players.Count;
    public bool IsGameStarted => _gameState.IsStarted;

    public GameRoom(string code, Session owner)
    {
        LobbyCode = code;
        OwnerId = owner.PlayerId;
        AddPlayer(owner);
    }

    public bool AddPlayer(Session session)
    {
        if (_players.Count >= 4 || _gameState.IsStarted)
        {
            return false;
        }

        _players.Add(session);
        session.CurrentLobbyCode = LobbyCode;
        return true;
    }

    public void RemovePlayer(string playerId)
    {
        var p = _players.FirstOrDefault(x => x.PlayerId == playerId);
        if (p != null)
        {
            _players.Remove(p);
        }
    }

    public async Task BroadcastState()
    {
        var update = new LobbyStateUpdate
        {
            PlayerNames = _players.Select(p => p.PlayerName).ToList()
        };

        foreach (var p in _players)
        {
            await p.SendAsync(MessageType.LobbyState, update);
        }
    }

    public async Task StartGame()
    {
        if (_players.Count < 2)
        {
            var owner = _players.FirstOrDefault(p => p.PlayerId == OwnerId);
            if (owner != null)
            {
                await owner.SendAsync(MessageType.Error,
                    new ErrorResponse { Message = "Need at least 2 players to start the game" });
            }

            return;
        }

        lock (_gameLock)
        {
            if (_gameState.IsStarted)
            {
                return;
            }

            // Initialize game with fixed player order
            var playerIds = _players.Select(p => p.PlayerId).ToList();
            _gameState.InitializeGame(playerIds);

            // Deal cards
            _deck.ReturnCards(new List<Card>());
            _gameState.DealCards(_deck);
        }

        // Notify all
        var startPlayerIds = _players.Select(p => p.PlayerId).ToList();
        var startPlayerNames = _players.Select(p => p.PlayerName).ToList();
        var startEvent = new GameStartedEvent
        {
            PlayerIds = startPlayerIds,
            PlayerNames = startPlayerNames
        };

        foreach (var p in _players)
        {
            await p.SendAsync(MessageType.GameStarted, startEvent);
        }

        await BroadcastGameState();
    }

    public async Task HandleClaimRank(Session session, ClaimRankRequest request)
    {
        lock (_gameLock)
        {
            if (!_gameState.IsStarted ||
                _gameState.IsGameEnded ||
                _gameState.Phase != RoundPhase.WaitingForClaim ||
                _gameState.GetCurrentPlayerId() != session.PlayerId)
            {
                return;
            }

            try
            {
                _gameState.ClaimRank(request.Rank);
            }
            catch (Exception ex)
            {
                Task.Run(async () =>
                    await session.SendAsync(MessageType.Error, new ErrorResponse { Message = ex.Message }));
                return;
            }
        }

        // Broadcast round started
        var roundStartEvent = new RoundStartedEvent
        {
            StartingPlayerId = session.PlayerId,
            ClaimedRank = request.Rank
        };

        foreach (var p in _players)
        {
            await p.SendAsync(MessageType.RoundStartedEvent, roundStartEvent);
        }

        await BroadcastGameState();
    }

    public async Task HandlePutDownCards(Session session, PutDownCardsRequest request)
    {
        lock (_gameLock)
        {
            if (!_gameState.IsStarted ||
                _gameState.IsGameEnded ||
                _gameState.Phase != RoundPhase.Playing ||
                _gameState.GetCurrentPlayerId() != session.PlayerId)
            {
                return;
            }

            try
            {
                _gameState.PutDownCards(session.PlayerId, request.CardIndices);
            }
            catch (Exception ex)
            {
                Task.Run(async () =>
                    await session.SendAsync(MessageType.Error, new ErrorResponse { Message = ex.Message }));
                return;
            }
        }

        // Send public event to others 
        var putDownEvent = new CardsPutDownEvent
        {
            PlayerId = session.PlayerId,
            CardCount = request.CardIndices.Count
        };

        foreach (var p in _players.Where(p => p.PlayerId != session.PlayerId))
        {
            await p.SendAsync(MessageType.CardsPutDownEvent, putDownEvent);
        }

        await BroadcastGameState();
    }

    public async Task HandleCallBluff(Session session, CallBluffRequest request)
    {
        bool gameEnded = false;
        bool immediateWin = false;
        string? winnerId = null;
        string loserId;
        Card revealedCard;
        bool isBluff;

        lock (_gameLock)
        {
            if (!_gameState.IsStarted ||
                _gameState.IsGameEnded ||
                _gameState.Phase != RoundPhase.Playing && _gameState.Phase != RoundPhase.CheckingBluff ||
                _gameState.GetCurrentPlayerId() != session.PlayerId)
            {
                return;
            }

            try
            {
                (isBluff, loserId, revealedCard, immediateWin) =
                    _gameState.CallBluff(session.PlayerId, request.CardIndexToCheck);
            }
            catch (Exception ex)
            {
                Task.Run(async () =>
                    await session.SendAsync(MessageType.Error, new ErrorResponse { Message = ex.Message }));
                return;
            }

            if (immediateWin)
            {
                gameEnded = true;
                winnerId = _gameState.WinnerId;
            }
        }

        // Broadcast bluff called event
        var bluffEvent = new BluffCalledEvent
        {
            CallerId = session.PlayerId,
            CardIndex = request.CardIndexToCheck,
            RevealedCard = new CardInfo { Rank = (int)revealedCard.Rank, Suit = revealedCard.Suit },
            IsBluff = isBluff,
            LoserId = loserId
        };

        foreach (var p in _players)
        {
            await p.SendAsync(MessageType.BluffCalledEvent, bluffEvent);
        }

        // End round
        List<int> discardedRanks = new();
        int cardsPickedUp = 0;
        string nextRoundStarterId = string.Empty;

        if (!immediateWin)
        {
            lock (_gameLock)
            {
                cardsPickedUp = _gameState.CardsInPlay.Count;
                discardedRanks = _gameState.EndRound(loserId).Select(r => (int)r).ToList();
                nextRoundStarterId = _gameState.GetCurrentPlayerId();

                if (_gameState.CheckWinCondition())
                {
                    gameEnded = true;
                    winnerId = _gameState.WinnerId;
                }
            }
        }

        if (gameEnded && winnerId != null)
        {
            var gameEndEvent = new GameEndedEvent
            {
                WinnerId = winnerId
            };

            foreach (var p in _players)
            {
                await p.SendAsync(MessageType.GameEndedEvent, gameEndEvent);
            }
        }
        else
        {
            var roundEndEvent = new RoundEndedEvent
            {
                LoserId = loserId,
                TotalCardsPickedUp = cardsPickedUp,
                NextRoundStarterId = nextRoundStarterId,
                DiscardedRanks = discardedRanks
            };

            foreach (var p in _players)
            {
                await p.SendAsync(MessageType.RoundEndedEvent, roundEndEvent);
            }

            await BroadcastGameState();
        }
    }

    private async Task BroadcastGameState()
    {
        foreach (var player in _players)
        {
            await SendPlayerSpecificState(player);
        }
    }


    private Task SendPlayerSpecificState(Session player)
    {
        lock (_gameLock)
        {
            if (!_gameState.IsStarted)
            {
                return Task.CompletedTask;
            }

            var ownHand = _gameState.GetPlayerHand(player.PlayerId);
            var handInfo = ownHand.Select(c => new CardInfo { Rank = (int)c.Rank, Suit = c.Suit }).ToList();

            var state = new GameStateUpdate
            {
                OwnHand = handInfo,
                PlayerHandCounts = _gameState.GetAllPlayerHandCounts(),
                ClaimedRank = _gameState.ClaimedRank.HasValue ? (int)_gameState.ClaimedRank.Value : null,
                CardsInPlayCounts = _gameState.GetCardsInPlayCounts(),
                CurrentPlayerId = _gameState.GetCurrentPlayerId(),
                Phase = _gameState.Phase.ToString(),
                LastBatchCount = _gameState.LastBatchCount
            };

            Task.Run(async () => await player.SendAsync(MessageType.GameStateUpdate, state));
        }

        return Task.CompletedTask;
    }

    public async Task HandlePlayerLeave(Session session)
    {
        bool gameWasActive;
        bool gameWasEnded;

        lock (_gameLock)
        {
            gameWasActive = _gameState.IsStarted;
            gameWasEnded = _gameState.IsGameEnded;
        }

        RemovePlayer(session.PlayerId);
        session.CurrentLobbyCode = null;

        // If game was active and not ended, end the game
        if (gameWasActive && !gameWasEnded)
        {
            var gameEndEvent = new GameEndedEvent
            {
                WinnerId = string.Empty
            };

            foreach (var p in _players)
            {
                await p.SendAsync(MessageType.GameEndedEvent, gameEndEvent);
            }
        }

        await BroadcastState();

        // Also send lobby state to the leaving player so they know they left
        try
        {
            var leaveUpdate = new LobbyStateUpdate
            {
                PlayerNames = _players.Select(p => p.PlayerName).ToList()
            };
            await session.SendAsync(MessageType.LobbyState, leaveUpdate);
        }
        catch
        {
            // connection already closed, ignore
        }
    }
}

public class LobbyManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

    public GameRoom? CreateRoom(Session owner)
    {
        string code = GenerateCode();
        var room = new GameRoom(code, owner);
        if (_rooms.TryAdd(code, room))
        {
            return room;
        }

        return null;
    }

    public GameRoom? JoinRoom(string code, Session player)
    {
        if (_rooms.TryGetValue(code, out var room))
        {
            if (room.AddPlayer(player))
            {
                return room;
            }
        }

        return null;
    }

    public GameRoom? GetRoom(string code)
    {
        _rooms.TryGetValue(code, out var room);
        return room;
    }

    private string GenerateCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 4).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}