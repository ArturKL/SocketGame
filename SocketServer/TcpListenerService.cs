using System.Net;
using System.Net.Sockets;
using GameEngine.Messaging;
using GameEngine.Transport;

namespace SocketServer;

public class TcpListenerService : INetworkListener
{
    private Socket? _socket;
    private bool _isRunning;
    private readonly IMessageSerializer _serializer;

    public event Action<IConnection>? ClientConnected;

    public TcpListenerService(IMessageSerializer serializer)
    {
        _serializer = serializer;
    }

    public void Start(int port)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _socket.Bind(new IPEndPoint(IPAddress.Any, port));
        _socket.Listen(10);

        _isRunning = true;
        Task.Run(AcceptLoop);
        Console.WriteLine($"Server started on port {port}");
    }

    public void Stop()
    {
        _isRunning = false;
        try
        {
            _socket?.Close();
        }
        catch
        {
            // ignored
        }
    }

    private async Task AcceptLoop()
    {
        while (_isRunning && _socket != null)
        {
            try
            {
                var clientSocket = await _socket.AcceptAsync();
                Console.WriteLine($"Accepted connection from {clientSocket.RemoteEndPoint}");

                var connection = new SocketConnection(clientSocket, _serializer, new LengthPrefixedFramer());
                ClientConnected?.Invoke(connection);
                connection.Start();
            }
            catch (ObjectDisposedException)
            {
                // Socket closed
                break;
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        }
    }
}