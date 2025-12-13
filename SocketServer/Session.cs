using GameEngine.Transport;
using GameEngine.Messaging;

namespace SocketServer;

public class Session
{
    public string SessionId { get; } = Guid.NewGuid().ToString();
    public string PlayerId { get; } = Guid.NewGuid().ToString();
    public string PlayerName { get; set; } = string.Empty;
    public IConnection Connection { get; }
    private readonly IMessageSerializer _serializer;

    public string? CurrentLobbyCode { get; set; }

    public Session(IConnection connection, IMessageSerializer serializer)
    {
        Connection = connection;
        _serializer = serializer;
    }

    public Task SendAsync<T>(MessageType messageType, T payload)
    {
        var env = new MessageEnvelope
        {
            MessageType = messageType,
            Payload = _serializer.SerializePayload(payload),
            SessionId = SessionId
        };
        return Connection.SendAsync(env);
    }
}