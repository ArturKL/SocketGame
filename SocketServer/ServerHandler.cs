using GameEngine.Messaging;
using GameEngine.Transport;
using SocketServer.Lobby;

namespace SocketServer;

public class ServerHandler
{
    private readonly LobbyManager _lobbyManager;
    private readonly Dictionary<string, Session> _sessions = new();
    private readonly IMessageSerializer _serializer;

    public ServerHandler(LobbyManager lobbyManager, IMessageSerializer serializer)
    {
        _lobbyManager = lobbyManager;
        _serializer = serializer;
    }

    public void HandleNewConnection(IConnection connection)
    {
        connection.OnMessageReceived += async (env) => await ProcessMessage(connection, env);
        connection.OnDisconnected += (reason) => HandleDisconnect(connection, reason);
    }

    private void HandleDisconnect(IConnection connection, string reason)
    {
        Console.WriteLine($"Client disconnected: {reason}");
        Session? session;
        lock (_sessions)
        {
            session = _sessions.Values.FirstOrDefault(s => s.Connection.ConnectionId == connection.ConnectionId);
            if (session != null)
            {
                _sessions.Remove(session.SessionId);
            }
        }

        if (session != null && session.CurrentLobbyCode != null)
        {
            var room = _lobbyManager.GetRoom(session.CurrentLobbyCode);
            if (room != null)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await room.HandlePlayerLeave(session);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error handling player leave: {ex}");
                    }
                });
            }
        }
    }

    private async Task ProcessMessage(IConnection connection, MessageEnvelope env)
    {
        try
        {
            Session? session = null;
            lock (_sessions)
            {
                if (env.SessionId != null && _sessions.ContainsKey(env.SessionId))
                {
                    var potentialSession = _sessions[env.SessionId];
                    if (potentialSession.Connection == connection)
                    {
                        session = potentialSession;
                    }
                }
            }

            // Handshake is special, doesn't need session yet
            if (env.MessageType == MessageType.HandshakeRequest)
            {
                var req = _serializer.DeserializePayload<HandshakeRequest>(env.Payload);
                if (req != null)
                {
                    lock (_sessions)
                    {
                        // Check for duplicate name
                        if (_sessions.Values.Any(s =>
                                s.PlayerName.Equals(req.PlayerName, StringComparison.OrdinalIgnoreCase)))
                        {
                            // Name taken
                            connection.SendAsync(new MessageEnvelope
                            {
                                MessageType = MessageType.Error,
                                Payload = _serializer.SerializePayload(new ErrorResponse
                                    { Message = "Name already taken" })
                            });
                            return;
                        }

                        var newSession = new Session(connection, _serializer) { PlayerName = req.PlayerName };
                        _sessions[newSession.SessionId] = newSession;

                        var resp = new HandshakeResponse
                        {
                            Success = true,
                            SessionId = newSession.SessionId,
                            PlayerId = newSession.PlayerId
                        };

                        connection.SendAsync(new MessageEnvelope
                        {
                            MessageType = MessageType.HandshakeResponse,
                            Payload = _serializer.SerializePayload(resp),
                            SessionId = newSession.SessionId
                        });
                    }
                }

                return;
            }

            if (session == null)
            {
                await connection.SendAsync(new MessageEnvelope
                {
                    MessageType = MessageType.Error,
                    Payload = _serializer.SerializePayload(new ErrorResponse { Message = "Unauthorized" })
                });
                return;
            }

            switch (env.MessageType)
            {
                case MessageType.CreateLobby:
                    var room = _lobbyManager.CreateRoom(session);
                    if (room != null)
                    {
                        await session.SendAsync(MessageType.LobbyCreated, new LobbyCreatedResponse
                        {
                            LobbyCode = room.LobbyCode,
                            OwnerId = session.PlayerId
                        });
                        await room.BroadcastState();
                    }

                    break;

                case MessageType.JoinLobby:
                    var joinReq = _serializer.DeserializePayload<JoinLobbyRequest>(env.Payload);
                    if (joinReq != null)
                    {
                        var joinedRoom = _lobbyManager.JoinRoom(joinReq.LobbyCode, session);
                        if (joinedRoom != null)
                        {
                            await joinedRoom.BroadcastState();
                        }
                        else
                        {
                            // Check why join failed
                            var existingRoom = _lobbyManager.GetRoom(joinReq.LobbyCode);
                            if (existingRoom == null)
                            {
                                await session.SendAsync(MessageType.Error,
                                    new ErrorResponse { Message = "Lobby not found" });
                            }
                            else if (existingRoom.PlayerCount >= 4)
                            {
                                await session.SendAsync(MessageType.Error,
                                    new ErrorResponse { Message = "Lobby is full (maximum 4 players)" });
                            }
                            else if (existingRoom.IsGameStarted)
                            {
                                await session.SendAsync(MessageType.Error,
                                    new ErrorResponse { Message = "Game has already started in this lobby" });
                            }
                            else
                            {
                                await session.SendAsync(MessageType.Error,
                                    new ErrorResponse { Message = "Unable to join lobby" });
                            }
                        }
                    }

                    break;

                case MessageType.StartGame:
                    if (session.CurrentLobbyCode != null)
                    {
                        var myRoom = _lobbyManager.GetRoom(session.CurrentLobbyCode);
                        if (myRoom != null && myRoom.OwnerId == session.PlayerId)
                        {
                            await myRoom.StartGame();
                        }
                    }

                    break;
                case MessageType.ClaimRankRequest:
                    if (session.CurrentLobbyCode != null)
                    {
                        var myRoom = _lobbyManager.GetRoom(session.CurrentLobbyCode);
                        var claimReq = _serializer.DeserializePayload<ClaimRankRequest>(env.Payload);
                        if (myRoom != null && claimReq != null)
                        {
                            await myRoom.HandleClaimRank(session, claimReq);
                        }
                    }

                    break;

                case MessageType.PutDownCardsRequest:
                    if (session.CurrentLobbyCode != null)
                    {
                        var myRoom = _lobbyManager.GetRoom(session.CurrentLobbyCode);
                        var putDownReq = _serializer.DeserializePayload<PutDownCardsRequest>(env.Payload);
                        if (myRoom != null && putDownReq != null)
                        {
                            await myRoom.HandlePutDownCards(session, putDownReq);
                        }
                    }

                    break;

                case MessageType.CallBluffRequest:
                    if (session.CurrentLobbyCode != null)
                    {
                        var myRoom = _lobbyManager.GetRoom(session.CurrentLobbyCode);
                        var bluffReq = _serializer.DeserializePayload<CallBluffRequest>(env.Payload);
                        if (myRoom != null && bluffReq != null)
                        {
                            await myRoom.HandleCallBluff(session, bluffReq);
                        }
                    }

                    break;

                case MessageType.LeaveLobby:
                    if (session.CurrentLobbyCode != null)
                    {
                        var myRoom = _lobbyManager.GetRoom(session.CurrentLobbyCode);
                        if (myRoom != null)
                        {
                            await myRoom.HandlePlayerLeave(session);
                        }
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex}");
        }
    }
}