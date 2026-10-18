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
        
        CheckTimeOut(clients);
        
        if (packet == null || packet.Type == PacketType.Error)
        {
            Console.WriteLine($"[{from}] broken datagram, skipped");
            continue;
        }
        
        var r = new PacketReader(packet.Payload);
        var pw = new PacketWriter();

        if (packet.Type == PacketType.Hello)
        {
            Console.WriteLine(r.ReadString()); 
            continue;
        }
        
        if (clients.Keys.Count >= 5 && !clients.ContainsKey(from))
        {
            var w = new PacketWriter();
            w.WriteString("Комната заполнена");
            await PacketIO.SendPacketAsync(socketServer, from, PacketType.Error, 0, w.ToArray());
            clients.Remove(from);
            continue;
        }

        try
        {
            var text = r.ReadString();
            r.EnsureEnd();
            clients[from] = Environment.TickCount64; //храним последнее время ответа
            Console.WriteLine($"[{from}] {text}");
            pw.WriteString("эхо: " + text);
            await PacketIO.SendPacketAsync(socketServer, from, packet.Type, 0, pw.ToArray());
        }
        catch (MalformedPacketException e)
        {
            Console.WriteLine($"{from} {e.Message}");
        }
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
void CheckTimeOut(Dictionary<IPEndPoint, long> clients)
{
    foreach (var client in clients.Keys)
    {
        if (Environment.TickCount64 - clients[client] > 10000)
        {
            clients.Remove(client);
        }
    }
}
