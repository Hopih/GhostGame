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

// Комнаты меняют и цикл приёма (JoinRoom…), и фоновая задача (таймаут игрока в комнате).
// Поэтому весь доступ к rooms идёт только внутри roomsGate — по одному, и внутри можно await.
var rooms = new Dictionary<int, Room>();
var roomsGate = new SemaphoreSlim(1, 1);
const int MaxClients = 40; // 8 комнат по 5 игроков (ТЗ 4)
const int MaxRooms = 8;

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
            await RemoveFromRoomAsync(session);
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
        case PacketType.ListRooms:
        case PacketType.CreateRoom:
        case PacketType.JoinRoom:
        case PacketType.LeaveRoom:
        case PacketType.SetReady:
        case PacketType.Chat:
        case PacketType.GameStart:
        case PacketType.RoomState:
        case PacketType.RoomList:   
            await HandleRoomPacketAsync(session, from, packet);
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

    if (clients.Values.Count(s => s.IsJoined) >= MaxClients)
    {
        await session.Channel.SendAsync(PacketType.Error,
            Packets.WriteError(ErrorCode.RoomFull, "Сервер заполнен"));
        return;
    }

    session.Join(nick);
    await session.Channel.SendAsync(PacketType.Welcome, Packets.WriteWelcome(session.PlayerId));
    Console.WriteLine($"[{from}] {nick} подключился, id {session.PlayerId}");
}

// Всё про комнаты: список, создание, вход, выход, готовность, чат комнаты (ТЗ 4.2)
async Task HandleRoomPacketAsync(ClientSession session, IPEndPoint from, Packet packet)
{
    await roomsGate.WaitAsync();
    try
    {
        rooms.TryGetValue(session.RoomId, out var room); // комната игрока или null, если он в лобби

        switch (packet.Type)
        {
            case PacketType.ListRooms:
                var infos = rooms.Values.Select(r => r.ToInfo()).ToList();
                await session.Channel.SendAsync(PacketType.RoomList, Packets.WriteRoomList(infos));
                break;

            case PacketType.CreateRoom:
                var name = Packets.ReadCreateRoom(packet.Payload);
                if (room != null)
                {
                    await SendErrorAsync(session, ErrorCode.UnexpectedPacket, "Вы уже в комнате");
                    break;
                }
                if (name.Length < 1 || name.Length > 24)
                {
                    await SendErrorAsync(session, ErrorCode.UnexpectedPacket, "Название комнаты — от 1 до 24 символов");
                    break;
                }
                if (rooms.Count >= MaxRooms)
                {
                    await SendErrorAsync(session, ErrorCode.RoomFull, "Достигнут лимит комнат");
                    break;
                }
                var created = new Room(name);
                rooms[created.Id] = created;
                created.Add(session);
                Console.WriteLine($"[{from}] {session.Nick} создал комнату «{created.Name}» (id {created.Id})");
                await created.BroadcastStateAsync();
                break;

            case PacketType.JoinRoom:
                var roomId = Packets.ReadJoinRoom(packet.Payload);
                if (room != null)
                {
                    await SendErrorAsync(session, ErrorCode.UnexpectedPacket, "Вы уже в комнате");
                    break;
                }
                if (!rooms.TryGetValue(roomId, out var target))
                {
                    await SendErrorAsync(session, ErrorCode.RoomNotFound, "Комната не найдена");
                    break;
                }
                if (target.IsFull)
                {
                    await SendErrorAsync(session, ErrorCode.RoomFull, "Комната заполнена");
                    break;
                }
                if (target.InGame)
                {
                    await SendErrorAsync(session, ErrorCode.GameInProgress, "Игра уже идёт");
                    break;
                }
                target.Add(session);
                Console.WriteLine($"[{from}] {session.Nick} вошёл в комнату «{target.Name}»");
                await target.BroadcastStateAsync();
                break;

            case PacketType.LeaveRoom:
                if (room == null)
                {
                    await SendErrorAsync(session, ErrorCode.UnexpectedPacket, "Вы не в комнате");
                    break;
                }
                await LeaveRoomAsync(session, room);
                break;

            case PacketType.SetReady:
                var ready = Packets.ReadSetReady(packet.Payload);
                if (room == null)
                {
                    await SendErrorAsync(session, ErrorCode.UnexpectedPacket, "Вы не в комнате");
                    break;
                }
                if (room.InGame)
                {
                    await SendErrorAsync(session, ErrorCode.GameInProgress, "Игра уже идёт");
                    break;
                }
                session.IsReady = ready;
                await room.BroadcastStateAsync();
                if (room.CanStart)
                {
                    var ghost = await room.StartGameAsync();
                    Console.WriteLine($"Комната «{room.Name}»: раунд начался, призрак — {ghost.Nick}");
                }
                break;

            case PacketType.Chat:
                var text = Packets.ReadChat(packet.Payload);
                if (room == null)
                {
                    await SendErrorAsync(session, ErrorCode.UnexpectedPacket, "Чат работает только в комнате");
                    break;
                }
                Console.WriteLine($"[{from}] «{room.Name}» {session.Nick}: {text}");
                await room.BroadcastAsync(PacketType.ChatMessage, Packets.WriteChatMessage(session.PlayerId, text));
                break;
            
            case PacketType.GameStart:
                var (role , roundSeconds , spawns) = Packets.ReadGameStart(packet.Payload);
                await session.Channel.SendAsync(PacketType.ChatMessage, Packets.WriteChatMessage(session.PlayerId, $"Игра началась! Ваща роль {role}"));
                break;
        }
    }
    finally
    {
        roomsGate.Release(); // finally: даже если вылетело исключение, иначе комнаты встанут навсегда
    }
}

// Выход из комнаты. Вызывать только внутри roomsGate.
async Task LeaveRoomAsync(ClientSession session, Room room)
{
    room.Remove(session);
    Console.WriteLine($"{session.Nick} вышел из комнаты «{room.Name}»");
    if (room.IsEmpty)
    {
        rooms.Remove(room.Id);
        Console.WriteLine($"Комната «{room.Name}» удалена");
        return;
    }
    await room.BroadcastStateAsync();
}

// Игрок отключился (Disconnect или таймаут) — убрать его из комнаты, если он в ней был
async Task RemoveFromRoomAsync(ClientSession session)
{
    await roomsGate.WaitAsync();
    try
    {
        if (rooms.TryGetValue(session.RoomId, out var room))
        {
            await LeaveRoomAsync(session, room);
        }
    }
    finally
    {
        roomsGate.Release();
    }
}

Task SendErrorAsync(ClientSession session, ErrorCode code, string message)
{
    return session.Channel.SendAsync(PacketType.Error, Packets.WriteError(code, message));
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
                    await RemoveFromRoomAsync(session);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[{endpoint}] maintenance error: {e.Message}");
            }
        }
    }
}
