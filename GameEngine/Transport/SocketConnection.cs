using System.Net;
using System.Net.Sockets;
using GameEngine.Messaging;

namespace GameEngine.Transport;

public class SocketConnection : IConnection
{
    private readonly Socket _socket;
    private readonly IMessageSerializer _serializer;
    private readonly IMessageFramer _framer;
    private readonly CancellationTokenSource _cts = new();

    public string ConnectionId { get; } = Guid.NewGuid().ToString();
    public bool IsConnected => _socket.Connected;
    public IPEndPoint? RemoteEndPoint => _socket.RemoteEndPoint as IPEndPoint;

    public event Func<MessageEnvelope, Task>? OnMessageReceived;
    public event Action<string>? OnDisconnected;
    public event Action<Exception>? OnError;

    public SocketConnection(Socket socket, IMessageSerializer serializer, IMessageFramer framer)
    {
        _socket = socket;
        _serializer = serializer;
        _framer = framer;
    }

    public void Start()
    {
        Task.Run(ReceiveLoop);
    }

    private async Task ReceiveLoop()
    {
        byte[] buffer = new byte[8192];
        try
        {
            while (!_cts.Token.IsCancellationRequested && _socket.Connected)
            {
                int received = await _socket.ReceiveAsync(buffer, SocketFlags.None, _cts.Token);
                if (received == 0)
                {
                    // Graceful close
                    OnDisconnected?.Invoke("Remote closed connection");
                    break;
                }

                var frames = _framer.UnframeData(buffer.AsSpan(0, received));
                foreach (var frame in frames)
                {
                    try
                    {
                        var envelope = _serializer.Deserialize(frame);
                        if (envelope != null && OnMessageReceived != null)
                        {
                            await OnMessageReceived.Invoke(envelope);
                        }
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(new Exception("Processing error", ex));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException)
        {
            OnDisconnected?.Invoke("Socket connection lost");
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
            OnDisconnected?.Invoke("Error in receive loop");
        }
        finally
        {
            await CloseAsync();
        }
    }

    public async Task SendAsync(MessageEnvelope envelope)
    {
        try
        {
            byte[] serialized = _serializer.Serialize(envelope);
            byte[] framed = _framer.FrameData(serialized);
            await _socket.SendAsync(framed, SocketFlags.None, _cts.Token);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    public Task CloseAsync()
    {
        if (_cts.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        _cts.Cancel();
        try
        {
            _socket.Shutdown(SocketShutdown.Both);
        }
        catch
        {
            // ignored
        }

        _socket.Close();
        return Task.CompletedTask;
    }
}