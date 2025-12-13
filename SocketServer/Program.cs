using GameEngine.Messaging;
using SocketServer;
using SocketServer.Lobby;

var serializer = new BinaryMessageSerializer();
var lobbyManager = new LobbyManager();
var handler = new ServerHandler(lobbyManager, serializer);
var listener = new TcpListenerService(serializer);

listener.ClientConnected += handler.HandleNewConnection;

listener.Start(5000);

Console.WriteLine("Press any key to stop...");
Console.ReadKey();

listener.Stop();