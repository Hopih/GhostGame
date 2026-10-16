using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using GhostGame.Shared;

var port = args.Length > 0 ? int.Parse(args[0]) : 5000;

var socketServer = new Socket(
    AddressFamily.InterNetwork,
    SocketType.Dgram,
    ProtocolType.Udp);

PacketIO.DisableConnectionReset(socketServer);
socketServer.Bind(new IPEndPoint(IPAddress.Any, port));
Console.WriteLine($"Server listening on UDP port {port}");

var buffer = new byte[PacketIO.ReceiveBufferSize];
Dictionary<IPEndPoint , long> clients = new Dictionary<IPEndPoint, long>();

while (true)
{
    try
    {
        var (packet, from) = await PacketIO.ReceivePacketAsync(socketServer, buffer);
        if (packet == null)
        {
            Console.WriteLine($"[{from}] broken datagram, skipped");
            continue;
        }
        
        clients[from] = Environment.TickCount64; //храним последнее время ответа
        if (clients.Keys.Count > 5)
        {
            await PacketIO.SendPacketAsync(socketServer, from, 0xF0, 0, Encoding.UTF8.GetBytes("Сервер заполнен"));
            clients.Remove(from);
            continue;
        }
            
        var text = Encoding.UTF8.GetString(packet.Payload);
        Console.WriteLine($"[{from}] {text}");

        await PacketIO.SendPacketAsync(socketServer, from, packet.Type, 0, Encoding.UTF8.GetBytes("эхо: " + text));
    }
    catch (SocketException e)
    {
        Console.WriteLine($"Socket error: {e.SocketErrorCode}");
    }
    catch (Exception e)
    {
        Console.WriteLine($"Error: {e.Message}");
    }
}
