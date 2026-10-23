using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using GhostGame.Server;
using GhostGame.Shared;

var port = args.Length > 0 ? int.Parse(args[0]) : 5000;

var socketServer = new Socket(
    AddressFamily.InterNetwork,
    SocketType.Dgram,
    ProtocolType.Udp);

PacketIO.DisableConnectionReset(socketServer);
socketServer.Bind(new IPEndPoint(IPAddress.Any, port));
var transport = new Transport(socketServer);
Console.WriteLine($"Server listening on UDP port {port}");

var buffer = new byte[PacketIO.ReceiveBufferSize];
// цикл приёма и фоновая задача работают параллельно, поэтому ConcurrentDictionary (ТЗ 4.1)
var clients = new ConcurrentDictionary<IPEndPoint, ClientSession>();

_ = Task.Run(MaintenanceLoopAsync);

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

        if (!clients.TryGetValue(from, out var session))
        {
            if (packet.Type != PacketType.Hello)
            {
                // незнакомый адрес и не Hello — молча отбрасываем (ТЗ 4.5)
                continue;
            }
            session = clients.GetOrAdd(from, endpoint => new ClientSession(new ReliableChannel(transport, endpoint)));
        }
        session.LastPing = Environment.TickCount64; //храним последнее время ответа

        try
        {
            foreach (var ready in await session.Channel.ReceiveAsync(packet))
            {
                await HandlePacketAsync(session, from, ready);
            }
        }
        catch (MalformedPacketException e)
        {
            Console.WriteLine($"[{from}] broken datagram: {e.Message}");
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

async Task HandlePacketAsync(ClientSession session, IPEndPoint from, Packet packet)
{
    if (packet.Type == PacketType.ClientDisconnect)
    {
        if (clients.TryRemove(from, out _))
        {
            Console.WriteLine($"Игрок {session.Nick ?? from.ToString()} отключился (Disconnect)");
        }
        return;
    }

    if (packet.Type == PacketType.Ping)
    {
        // Ping ходит на любом этапе и заодно не даёт сессии истечь (ТЗ 3.7)
        await transport.SendUnreliableAsync(PacketType.Pong, Packets.WritePong(Packets.ReadPing(packet.Payload)), from);
        return;
    }

    if (packet.Type == PacketType.Hello)
    {
        await HandleHelloAsync(session, from, packet);
        return;
    }

    if (!session.IsJoined)
    {
        await session.Channel.SendAsync(PacketType.Error,
            Packets.WriteError(ErrorCode.UnexpectedPacket, "Сначала Hello"));
        return;
    }

    switch (packet.Type)
    {
        case PacketType.Chat:
            var text = Packets.ReadChat(packet.Payload);
            Console.WriteLine($"[{from}] {session.Nick}: {text}");
            foreach (var other in clients.Values.Where(c => c.IsJoined))
            {
                await other.Channel.SendAsync(PacketType.ChatMessage, Packets.WriteChatMessage(session.PlayerId, text));
            }
            break;

        default:
            Console.WriteLine($"[{from}] unexpected packet {packet.Type}");
            await session.Channel.SendAsync(PacketType.Error,
                Packets.WriteError(ErrorCode.UnexpectedPacket, $"Пакет {packet.Type} не ожидается"));
            break;
    }
}

async Task HandleHelloAsync(ClientSession session, IPEndPoint from, Packet packet)
{
    var (version, nick) = Packets.ReadHello(packet.Payload);

    if (session.IsJoined)
    {
        // новый Hello от уже принятого игрока: второго игрока не создаём (ТЗ 3.7)
        await session.Channel.SendAsync(PacketType.Welcome, Packets.WriteWelcome(session.PlayerId));
        return;
    }

    if (version != 1)
    {
        await session.Channel.SendAsync(PacketType.Error,
            Packets.WriteError(ErrorCode.UnsupportedVersion, "Версия не поддерживается"));
        return;
    }

    if (nick.Length < 3 || nick.Length > 16 || clients.Values.Any(s => s.Nick == nick))
    {
        await session.Channel.SendAsync(PacketType.Error,
            Packets.WriteError(ErrorCode.BadNick, "Ник занят или некорректен"));
        return;
    }

    if (clients.Values.Count(s => s.IsJoined) >= 5)
    {
        await session.Channel.SendAsync(PacketType.Error,
            Packets.WriteError(ErrorCode.RoomFull, "Комната заполнена"));
        return;
    }

    session.Join(nick);
    await session.Channel.SendAsync(PacketType.Welcome, Packets.WriteWelcome(session.PlayerId));
    Console.WriteLine($"[{from}] {nick} подключился, id {session.PlayerId}");
}

// Фоновая задача раз в 50 мс (ТЗ 4.1): повторы надёжного канала и таймауты сессий
async Task MaintenanceLoopAsync()
{
    while (true)
    {
        await Task.Delay(50);
        foreach (var (endpoint, session) in clients)
        {
            try
            {
                var (resent, failed) = await session.Channel.ResendAsync();
                if (resent > 0)
                {
                    Console.WriteLine($"[{endpoint}] повторов: {resent} (всего {session.Channel.TotalResent})");
                }

                var silent = Environment.TickCount64 - session.LastPing > 10_000;
                if ((failed || silent) && clients.TryRemove(endpoint, out _))
                {
                    var reason = failed ? "нет подтверждений 10 с" : "таймаут";
                    Console.WriteLine($"Игрок {session.Nick ?? endpoint.ToString()} отключен: {reason}");
                    for (var i = 0; i < 3; i++)
                    {
                        await transport.SendUnreliableAsync(PacketType.ServerDisconnect,
                            Packets.WriteServerDisconnect(DisconnectReason.Timeout), endpoint);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[{endpoint}] maintenance error: {e.Message}");
            }
        }
    }
}
