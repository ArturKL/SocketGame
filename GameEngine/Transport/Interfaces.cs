using System.Net;
using GameEngine.Messaging;

namespace GameEngine.Transport;

public interface INetworkListener
{
    void Start(int port);
    void Stop();
    event Action<IConnection> ClientConnected;
}

public interface IConnection
{
    string ConnectionId { get; }
    bool IsConnected { get; }
    IPEndPoint? RemoteEndPoint { get; }

    Task SendAsync(MessageEnvelope envelope);
    Task CloseAsync();

    event Func<MessageEnvelope, Task> OnMessageReceived;
    event Action<string> OnDisconnected;
    event Action<Exception> OnError;
}

public interface IMessageFramer
{
    IEnumerable<byte[]> UnframeData(ReadOnlySpan<byte> data);
    byte[] FrameData(byte[] data);
}