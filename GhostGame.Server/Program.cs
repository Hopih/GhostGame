using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using GhostGame.Server;
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
Dictionary<IPEndPoint , ClientSession> clients = new Dictionary<IPEndPoint, ClientSession>();

while (true)
{
    await CheckTimeOut(clients);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
    
    try
    {
        var (packet, from) = await PacketIO.ReceivePacketAsync(socketServer, buffer , cts.Token);
        
        if (packet == null || packet.Type == PacketType.Error)
        {
            Console.WriteLine($"[{from}] broken datagram, skipped");
            continue;
        }
        
        
        if (clients.Keys.Count >= 5 && !clients.ContainsKey(from))
        {
            await PacketIO.SendPacketAsync(socketServer, from, PacketType.Error, 0,
                Packets.WriteError(ErrorCode.RoomFull, "Комната заполнена"));
            continue;
        }
        
        try
        {
            if (packet.Type == PacketType.Hello)
            {
                var (version, nick) = Packets.ReadHello(packet.Payload);
                Console.WriteLine($"[{from}] Hello: {nick}, version {version}");
                clients[from] = new ClientSession(nick);
                continue;
            }
            
            if (!clients.ContainsKey(from))
            {
                continue;
            }

            var text = Packets.ReadChat(packet.Payload);
            clients[from].LastPing = Environment.TickCount64; //храним последнее время ответа
            Console.WriteLine($"[{from}] {text}");
            await PacketIO.SendPacketAsync(socketServer, from, packet.Type, 0, Packets.WriteChat("эхо: " + text));
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
async Task CheckTimeOut(Dictionary<IPEndPoint, ClientSession> clients)
{
    foreach (var client in clients.Keys)
    {
        if (Environment.TickCount64 - clients[client].LastPing > 10000)
        {
            Console.WriteLine($"Игрок {clients[client].Nick} отключен");
            await PacketIO.SendPacketAsync(socketServer, client, PacketType.Disconnect , 0, Packets.WriteChat("Вы отключены"));
            clients.Remove(client);
        }
    }
}

