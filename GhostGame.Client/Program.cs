using System.Net;
using System.Net.Sockets;
using GhostGame.Client;
using GhostGame.Client.Graphics;
using GhostGame.Client.Screens;
using GhostGame.Shared;
using Raylib_cs;

var server = new IPEndPoint(
    args.Length > 0 ? IPAddress.Parse(args[0]) : IPAddress.Loopback,
    args.Length > 1 ? int.Parse(args[1]) : 5000);
var map = GameMap.Load();

// --- сеть: сокет, канал и очередь входящих создаются заново на каждое подключение ---
Socket? socket = null;
Transport? transport = null;
ReliableChannel? channel = null;
var incoming = System.Threading.Channels.Channel.CreateUnbounded<Packet>(); // приём кладёт сюда, главный поток разбирает (ТЗ 5.3)
var lastHeard = 0L;        // когда последний раз что-то пришло от сервера (пишет фоновый приём)
var pingMs = -1L;
var channelFailed = false; // надёжный пакет не подтверждён 10 с (ставит фоновая задача повторов)

// --- что сейчас на экране и что клиент знает ---
var screen = Screen.Connect;
var connected = false;      // Welcome получен
var helloSentAt = -1L;      // когда отправили Hello; -1 — Welcome не ждём
var myId = 0;
var status = "";            // сообщение внизу экрана: ошибка, «подключено» и т. п.
var statusUntil = 0L;
var rooms = new List<RoomInfo>();
var lastRoomsRequest = 0L;
(int RoomId, string Name, int HostId, List<RoomPlayer> Players)? room = null; // последний RoomState
var chat = new List<string>();
GameScreen? game = null;
uint inputSeq = 0;
var lastInputAt = 0L;
var lastPingAt = 0L;

Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
// Окно открывается развёрнутым на весь экран (окно 1920×1080 на экране 1920×1080 не влезает из-за заголовка и панели задач).
// Его можно свернуть в окно и растягивать — экраны раскладываются от размера окна. MSAA сглаживает края кругов.
Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint);
Raylib.InitWindow(1600, 900, "Призрак и охотники");
Raylib.MaximizeWindow();
Raylib.SetWindowMinSize(1240, 780);
Raylib.SetTargetFPS(60);
Raylib.SetExitKey(KeyboardKey.Null); // Esc не закрывает окно
Assets.Load(); // спрайты и шрифт — только после InitWindow

// Экраны меню: только рисуют и сообщают, что нажали. Что отправить серверу, решает этот файл.
var connectScreen = new ConnectScreen();
var lobbyScreen = new LobbyScreen();
var roomScreen = new RoomScreen();

// Главный цикл кадров. Всё, что связано с окном, — только здесь, в главном потоке (ТЗ 5.3).
// Поэтому здесь нет await: после await код мог бы продолжиться в другом потоке, а Raylib работает только в этом.
while (!Raylib.WindowShouldClose())
{
    var now = Environment.TickCount64;
    HandlePackets();
    CheckConnection(now);

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Theme.Background);
    switch (screen)
    {
        case Screen.Connect: ConnectFrame(); break;
        case Screen.Lobby: LobbyFrame(now); break;
        case Screen.Room: RoomFrame(); break;
        case Screen.Game: GameFrame(now); break;
    }
    StatusFrame(now);
    Raylib.EndDrawing();
}

// окно закрыли: 3 копии Disconnect — хотя бы одна должна дойти (ТЗ 3.7)
if (connected && transport != null)
{
    for (var i = 0; i < 3; i++)
    {
        transport.SendUnreliableAsync(PacketType.ClientDisconnect, Array.Empty<byte>(), server).Wait();
    }
}
Raylib.CloseWindow();
socket?.Close();

// ===================== сеть =====================

// Отправка из главного потока «запустил и пошёл дальше»: отправка UDP быстрая, ждать её кадру незачем
void Send(PacketType type, byte[] payload)
{
    if (channel != null)
    {
        _ = channel.SendAsync(type, payload);
    }
}

void SendUnreliable(PacketType type, byte[] payload)
{
    if (transport != null)
    {
        _ = transport.SendUnreliableAsync(type, payload, server);
    }
}

// Кнопка «Войти»: при первой попытке (и после обрыва) — новый сокет, канал и фоновые задачи; потом Hello
void Connect(string nick)
{
    if (channel == null)
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0)); // порт выбирает ОС — можно запустить несколько клиентов
        transport = new Transport(socket);
        channel = new ReliableChannel(transport, server);
        incoming = System.Threading.Channels.Channel.CreateUnbounded<Packet>();
        channelFailed = false;
        lastHeard = Environment.TickCount64;
        var (s, ch, inbox) = (socket, channel, incoming);
        _ = Task.Run(() => ReceiveLoopAsync(s, ch, inbox));
        _ = Task.Run(() => ResendLoopAsync(ch));
    }
    Send(PacketType.Hello, Packets.WriteHello(1, nick));
    helloSentAt = Environment.TickCount64;
    ShowStatus("Подключение…");
}

// Вернуться на экран подключения: сервер не отвечает, связь потеряна или сервер отключил
void Disconnect(string reason)
{
    connected = false;
    helloSentAt = -1;
    channel = null;      // фоновые задачи старого подключения увидят это и закончатся
    transport = null;
    socket?.Close();
    socket = null;
    room = null;
    game = null;
    rooms.Clear();
    chat.Clear();
    screen = Screen.Connect;
    ShowStatus(reason);
}

// Раз в кадр: Welcome за 5 с, Ping раз в секунду, 5 с тишины от сервера — «Соединение потеряно» (ТЗ 5.3)
void CheckConnection(long now)
{
    if (helloSentAt >= 0 && now - helloSentAt > 5000)
    {
        Disconnect("Сервер не отвечает");
        return;
    }
    if (!connected)
    {
        return;
    }
    if (now - lastPingAt >= 1000)
    {
        lastPingAt = now;
        SendUnreliable(PacketType.Ping, Packets.WritePing(now));
    }
    if (now - lastHeard > 5000 || channelFailed)
    {
        Disconnect("Соединение потеряно");
    }
}

// Разобрать всё, что пришло с прошлого кадра. TryRead не ждёт: пакетов нет — сразу дальше рисовать.
void HandlePackets()
{
    while (incoming.Reader.TryRead(out var packet))
    {
        try
        {
            HandlePacket(packet);
        }
        catch (MalformedPacketException)
        {
            // битый пакет от сервера пропускаем, следующий разберётся нормально
        }
    }
}

void HandlePacket(Packet packet)
{
    switch (packet.Type)
    {
        case PacketType.Welcome:
            myId = Packets.ReadWelcome(packet.Payload);
            connected = true;
            helloSentAt = -1;
            screen = Screen.Lobby;
            lobbyScreen.Focus();
            ShowStatus($"Подключено, ваш id: {myId}");
            RequestRooms();
            break;

        case PacketType.Error:
            var (_, message) = Packets.ReadError(packet.Payload);
            if (!connected)
            {
                helloSentAt = -1; // ответ на Hello: можно ввести другой ник
            }
            ShowStatus($"Ошибка: {message}");
            break;

        case PacketType.ServerDisconnect:
            var reason = Packets.ReadServerDisconnect(packet.Payload);
            Disconnect($"Сервер отключил вас: {(reason == DisconnectReason.Timeout ? "таймаут" : "сервер выключается")}");
            break;

        case PacketType.RoomList:
            rooms = Packets.ReadRoomList(packet.Payload);
            break;

        case PacketType.RoomState:
            room = Packets.ReadRoomState(packet.Payload);
            if (screen == Screen.Lobby)
            {
                screen = Screen.Room;
                roomScreen.Focus();
            }
            break;

        case PacketType.ChatMessage:
            var (playerId, text) = Packets.ReadChatMessage(packet.Payload);
            var author = room?.Players.FirstOrDefault(p => p.PlayerId == playerId)?.Nick ?? $"#{playerId}";
            chat.Add($"{author}: {text}");
            if (chat.Count > 50)
            {
                chat.RemoveAt(0);
            }
            break;

        case PacketType.GameStart:
            var (role, roundSeconds, spawns) = Packets.ReadGameStart(packet.Payload);
            var nicks = room?.Players.ToDictionary(p => p.PlayerId, p => p.Nick) ?? new Dictionary<int, string>();
            game = new GameScreen(map, myId, role, roundSeconds, spawns, nicks);
            inputSeq = 0;
            screen = Screen.Game;
            break;

        case PacketType.Snapshot:
            // Snapshot без гарантий и может обогнать GameStart — вне экрана игры его просто игнорируем (ТЗ 3.6)
            if (screen == Screen.Game && game != null)
            {
                game.ApplySnapshot(Packets.ReadSnapshot(packet.Payload));
            }
            break;
    }
}

void RequestRooms()
{
    lastRoomsRequest = Environment.TickCount64;
    Send(PacketType.ListRooms, Array.Empty<byte>());
}

void ShowStatus(string text)
{
    status = text;
    statusUntil = Environment.TickCount64 + 4000;
}

// Фоновый приём: сокет → надёжный канал → очередь incoming. Pong обрабатываем сразу.
async Task ReceiveLoopAsync(Socket s, ReliableChannel ch, System.Threading.Channels.Channel<Packet> inbox)
{
    var buffer = new byte[PacketIO.ReceiveBufferSize];
    while (true)
    {
        try
        {
            var (packet, from) = await PacketIO.ReceivePacketAsync(s, buffer);
            if (packet == null || !from.Equals(server))
            {
                continue;
            }
            lastHeard = Environment.TickCount64;

            if (packet.Type == PacketType.Pong)
            {
                pingMs = Environment.TickCount64 - Packets.ReadPong(packet.Payload);
                continue;
            }

            foreach (var ready in await ch.ReceiveAsync(packet))
            {
                inbox.Writer.TryWrite(ready);
            }
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset)
        {
            // Windows так сообщает, что на порту сервера никто не слушает; экран скажет «Сервер не отвечает»
        }
        catch (MalformedPacketException)
        {
            // битая датаграмма от сервера — пропускаем
        }
        catch (ObjectDisposedException)
        {
            return; // сокет закрыли — это подключение закончилось
        }
    }
}

// Фоновые повторы надёжного канала раз в 50 мс, пока это подключение текущее
async Task ResendLoopAsync(ReliableChannel ch)
{
    while (ch == channel)
    {
        await Task.Delay(50);
        var (_, failed) = await ch.ResendAsync();
        if (failed)
        {
            channelFailed = true; // главный поток увидит и вернётся на экран подключения
        }
    }
}

// ===================== экраны =====================
// Каждая функция — один кадр своего экрана: экран рисует себя и сообщает, что нажали,
// а здесь решаем, какой пакет отправить и куда перейти. Сами экраны про сеть ничего не знают.

void ConnectFrame()
{
    var nick = connectScreen.Draw(server.ToString(), waiting: helloSentAt >= 0);
    if (nick != null)
    {
        Connect(nick);
    }
}

void LobbyFrame(long now)
{
    if (now - lastRoomsRequest > 3000)
    {
        RequestRooms(); // список комнат сам обновляется раз в 3 с
    }

    switch (lobbyScreen.Draw(rooms, connectScreen.Nick, myId, pingMs))
    {
        case LobbyAction.Join join:
            Send(PacketType.JoinRoom, Packets.WriteJoinRoom(join.RoomId));
            break;
        case LobbyAction.Create create:
            Send(PacketType.CreateRoom, Packets.WriteCreateRoom(create.Name));
            break;
        case LobbyAction.Refresh:
            RequestRooms();
            break;
    }
}

void RoomFrame()
{
    if (room is not { } r)
    {
        return;
    }

    switch (roomScreen.Draw(r.Name, r.HostId, r.Players, myId, chat))
    {
        case RoomAction.ToggleReady:
            Send(PacketType.SetReady, Array.Empty<byte>()); // сервер переключит и пришлёт RoomState (ТЗ 3.4)
            break;
        case RoomAction.Leave:
            // выходящему сервер RoomState не присылает — он уже не в комнате, поэтому уходим в лобби сами
            Send(PacketType.LeaveRoom, Array.Empty<byte>());
            room = null;
            chat.Clear();
            screen = Screen.Lobby;
            lobbyScreen.Focus();
            RequestRooms();
            break;
        case RoomAction.Chat message:
            Send(PacketType.Chat, Packets.WriteChat(message.Text));
            break;
    }
}

void GameFrame(long now)
{
    if (game == null)
    {
        return;
    }

    // Input 20 раз в секунду независимо от частоты кадров, inputSeq растёт (ТЗ 5.3); без гарантий
    if (now - lastInputAt >= 50)
    {
        lastInputAt = now;
        SendUnreliable(PacketType.Input, Packets.WriteInput(++inputSeq, game.ReadInput()));
    }

    if (game.Draw(pingMs)) // время вышло и нажали «В комнату»
    {
        game = null;
        screen = room != null ? Screen.Room : Screen.Lobby;
    }
}

void StatusFrame(long now)
{
    if (screen == Screen.Game || now > statusUntil)
    {
        return;
    }
    var isError = status.StartsWith("Ошибка") || status.StartsWith("Сервер") || status.StartsWith("Соединение");
    Ui.StatusLine(status, isError);
}

enum Screen
{
    Connect,
    Lobby,
    Room,
    Game,
}
