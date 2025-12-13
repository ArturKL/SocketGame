using System.Net.Sockets;
using GameEngine.Game;
using GameEngine.Messaging;
using GameEngine.Transport;

namespace SocketClient;

public class GameClient
{
    private SocketConnection? _connection;
    private readonly BinaryMessageSerializer _serializer = new();

    public string PlayerName { get; private set; } = string.Empty;
    public string SessionId { get; private set; } = string.Empty;
    public string PlayerId { get; private set; } = string.Empty;
    public bool IsConnected => _connection?.IsConnected ?? false;

    // Events for UI
    public event Action<string>? OnLog;
    public event Action? OnConnected;
    public event Action<LobbyCreatedResponse>? OnLobbyCreated;
    public event Action<LobbyStateUpdate>? OnLobbyUpdate;
    public event Action<GameStartedEvent>? OnGameStarted;

    // Game events
    public event Action<GameStateUpdate>? OnGameStateUpdate;
    public event Action<CardsPutDownEvent>? OnCardsPutDown;
    public event Action<BluffCalledEvent>? OnBluffCalled;
    public event Action<RoundStartedEvent>? OnRoundStarted;
    public event Action<RoundEndedEvent>? OnRoundEnded;
    public event Action<GameEndedEvent>? OnGameEnded;
    public event Action? OnLeftLobby;
    public event Action<string>? OnError;

    public async Task ConnectAndLogin(string host, int port, string name)
    {
        PlayerName = name;
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(host, port);

        _connection = new SocketConnection(socket, _serializer, new LengthPrefixedFramer());
        _connection.OnMessageReceived += HandleMessage;
        _connection.OnDisconnected += (reason) => OnLog?.Invoke($"Disconnected: {reason}");
        _connection.Start();

        // Handshake
        var req = new HandshakeRequest { PlayerName = name };
        await _connection.SendAsync(new MessageEnvelope
        {
            MessageType = MessageType.HandshakeRequest,
            Payload = _serializer.SerializePayload(req)
        });
    }

    private Task HandleMessage(MessageEnvelope env)
    {
        switch (env.MessageType)
        {
            case MessageType.HandshakeResponse:
                var resp = _serializer.DeserializePayload<HandshakeResponse>(env.Payload);
                if (resp != null && resp.Success)
                {
                    SessionId = resp.SessionId;
                    PlayerId = resp.PlayerId;
                    OnLog?.Invoke("Connected and Logged in!");
                    OnConnected?.Invoke();
                }

                break;

            case MessageType.LobbyCreated:
                var lobby = _serializer.DeserializePayload<LobbyCreatedResponse>(env.Payload);
                if (lobby != null)
                {
                    OnLobbyCreated?.Invoke(lobby);
                }
                break;

            case MessageType.LobbyState:
                var state = _serializer.DeserializePayload<LobbyStateUpdate>(env.Payload);
                if (state != null)
                {
                    if (!state.PlayerNames.Contains(PlayerName))
                    {
                        OnLeftLobby?.Invoke();
                    }
                    else
                    {
                        OnLobbyUpdate?.Invoke(state);
                    }
                }

                break;

            case MessageType.GameStarted:
                var gameStart = _serializer.DeserializePayload<GameStartedEvent>(env.Payload);
                if (gameStart != null)
                {
                    OnGameStarted?.Invoke(gameStart);
                }

                break;
            case MessageType.Error:
                var err = _serializer.DeserializePayload<ErrorResponse>(env.Payload);
                if (err != null)
                {
                    OnError?.Invoke(err.Message);
                    OnLog?.Invoke($"Error: {err.Message}");
                }

                break;

            case MessageType.GameStateUpdate:
                var gameState = _serializer.DeserializePayload<GameStateUpdate>(env.Payload);
                if (gameState != null)
                {
                    OnGameStateUpdate?.Invoke(gameState);
                }
                break;

            case MessageType.CardsPutDownEvent:
                var putDown = _serializer.DeserializePayload<CardsPutDownEvent>(env.Payload);
                if (putDown != null)
                {
                    OnCardsPutDown?.Invoke(putDown);
                }
                break;

            case MessageType.BluffCalledEvent:
                var bluff = _serializer.DeserializePayload<BluffCalledEvent>(env.Payload);
                if (bluff != null)
                {
                    OnBluffCalled?.Invoke(bluff);
                }
                break;

            case MessageType.RoundStartedEvent:
                var roundStart = _serializer.DeserializePayload<RoundStartedEvent>(env.Payload);
                if (roundStart != null)
                {
                    OnRoundStarted?.Invoke(roundStart);
                }
                break;

            case MessageType.RoundEndedEvent:
                var roundEnd = _serializer.DeserializePayload<RoundEndedEvent>(env.Payload);
                if (roundEnd != null)
                {
                    OnRoundEnded?.Invoke(roundEnd);
                }
                break;

            case MessageType.GameEndedEvent:
                var gameEnd = _serializer.DeserializePayload<GameEndedEvent>(env.Payload);
                if (gameEnd != null)
                {
                    OnGameEnded?.Invoke(gameEnd);
                }
                break;
        }

        return Task.CompletedTask;
    }

    public async Task CreateLobby()
    {
        if (_connection == null)
        {
            return;
        }

        await _connection.SendAsync(new MessageEnvelope
        {
            MessageType = MessageType.CreateLobby,
            SessionId = SessionId
        });
    }

    public async Task JoinLobby(string code)
    {
        if (_connection == null)
        {
            return;
        }

        await _connection.SendAsync(new MessageEnvelope
        {
            MessageType = MessageType.JoinLobby,
            SessionId = SessionId,
            Payload = _serializer.SerializePayload(new JoinLobbyRequest { LobbyCode = code })
        });
    }

    public async Task StartGame()
    {
        if (_connection == null)
        {
            return;
        }

        await _connection.SendAsync(new MessageEnvelope
        {
            MessageType = MessageType.StartGame,
            SessionId = SessionId
        });
    }

    public async Task ClaimRank(int rank)
    {
        if (_connection == null)
        {
            return;
        }

        await _connection.SendAsync(new MessageEnvelope
        {
            MessageType = MessageType.ClaimRankRequest,
            SessionId = SessionId,
            Payload = _serializer.SerializePayload(new ClaimRankRequest { Rank = (Rank)rank })
        });
    }

    public async Task PutDownCards(List<int> cardIndices)
    {
        if (_connection == null)
        {
            return;
        }

        await _connection.SendAsync(new MessageEnvelope
        {
            MessageType = MessageType.PutDownCardsRequest,
            SessionId = SessionId,
            Payload = _serializer.SerializePayload(new PutDownCardsRequest { CardIndices = cardIndices })
        });
    }

    public async Task CallBluff(int cardIndexToCheck)
    {
        if (_connection == null)
        {
            return;
        }

        await _connection.SendAsync(new MessageEnvelope
        {
            MessageType = MessageType.CallBluffRequest,
            SessionId = SessionId,
            Payload = _serializer.SerializePayload(new CallBluffRequest { CardIndexToCheck = cardIndexToCheck })
        });
    }

    public async Task LeaveLobby()
    {
        if (_connection == null)
        {
            return;
        }

        await _connection.SendAsync(new MessageEnvelope
        {
            MessageType = MessageType.LeaveLobby,
            SessionId = SessionId,
            Payload = _serializer.SerializePayload(new LeaveLobbyRequest())
        });
    }
}