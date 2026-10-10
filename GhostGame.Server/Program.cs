using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using GhostGame.Shared;
var socketServer = new Socket(
    AddressFamily.InterNetwork,
    SocketType.Stream,
    ProtocolType.Tcp);

socketServer.Bind(
    new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), 5000));
socketServer.Listen();

while (true)
{
    var connectionSocket = await socketServer.AcceptAsync();
    _ = HandeClientAsync(connectionSocket);
}

async Task HandeClientAsync(Socket connectionSocket)
{
    Console.WriteLine("Client connected!");
    
    do
    {
        var result = await PacketIO.ReceivePacketAsync(connectionSocket);
        if (result == null)
        {
            break;
        }
        var text = Encoding.UTF8.GetString(result);
        Console.WriteLine(text);
        
        var bytesSent = await PacketIO.SendPacketAsync(connectionSocket, Encoding.UTF8.GetBytes("эхо: "  + text));
    } while (true);
}






