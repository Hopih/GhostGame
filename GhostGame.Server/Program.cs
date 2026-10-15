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
