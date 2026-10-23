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
        
        
        try
        {
            if (packet.Type == PacketType.Hello)
            {
                var (version, nick) = Packets.ReadHello(packet.Payload);

                if (clients.TryGetValue(from, out var existing))
                {
                    // повторный Hello с того же адреса: второго игрока не создаём (ТЗ 3.7)
                    existing.LastPing = Environment.TickCount64;
                    await PacketIO.SendPacketAsync(socketServer, from, PacketType.Welcome, 0,
                        Packets.WriteWelcome(existing.PlayerId));
                    continue;
                }

                if (version != 1)
                {
                    await PacketIO.SendPacketAsync(socketServer, from, PacketType.Error, 0,
                        Packets.WriteError(ErrorCode.UnsupportedVersion, "Версия не поддерживается"));
                    continue;
                }

                if (nick.Length < 3 || nick.Length > 16 || clients.Values.Any(s => s.Nick == nick))
                {
                    await PacketIO.SendPacketAsync(socketServer, from, PacketType.Error, 0,
                        Packets.WriteError(ErrorCode.BadNick, "Ник занят или некорректен"));
                    continue;
                }

                if (clients.Count >= 5)
                {
                    await PacketIO.SendPacketAsync(socketServer, from, PacketType.Error, 0,
                        Packets.WriteError(ErrorCode.RoomFull, "Комната заполнена"));
                    continue;
                }

                var newSession = new ClientSession(nick);
                clients[from] = newSession;
                await PacketIO.SendPacketAsync(socketServer, from, PacketType.Welcome, 0,
                    Packets.WriteWelcome(newSession.PlayerId));
                Console.WriteLine($"[{from}] {nick} подключился, id {newSession.PlayerId}");
                continue;
            }

            if (!clients.TryGetValue(from, out var session))
            {
                // незнакомый адрес и не Hello — молча отбрасываем (ТЗ 4.5)
                continue;
            }
            session.LastPing = Environment.TickCount64; //храним последнее время ответа

            switch (packet.Type)
            {
                case PacketType.Chat:
                    var text = Packets.ReadChat(packet.Payload);
                    Console.WriteLine($"[{from}] {text}");
                    await PacketIO.SendPacketAsync(socketServer, from, PacketType.Chat, 0, Packets.WriteChat("эхо: " + text));
                    break;

                default:
                    Console.WriteLine($"[{from}] unexpected packet {packet.Type}");
                    await PacketIO.SendPacketAsync(socketServer, from, PacketType.Error, 0,
                        Packets.WriteError(ErrorCode.UnexpectedPacket, $"Пакет {packet.Type} не ожидается"));
                    break;
            }
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
    catch (OperationCanceledException)
    {
        // за секунду ничего не пришло — это нормально, идём на новый круг
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
            Console.WriteLine($"Игрок {clients[client].Nick} отключен: таймаут");
            await PacketIO.SendPacketAsync(socketServer, client, PacketType.ServerDisconnect, 0,
                Packets.WriteServerDisconnect(DisconnectReason.Timeout));
            clients.Remove(client);
        }
    }
}

