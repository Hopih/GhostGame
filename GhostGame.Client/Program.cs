using System.Net;
using System.Net.Sockets;
using GhostGame.Client;
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
Raylib.InitWindow(1920, 1080, "Призрак и охотники");
Raylib.SetTargetFPS(60);
Raylib.SetExitKey(KeyboardKey.Null); // Esc не закрывает окно
Ui.LoadFont();

var nickInput = new TextInput(16, focused: true);
var roomNameInput = new TextInput(24);
var chatInput = new TextInput(200);

// Главный цикл кадров. Всё, что связано с окном, — только здесь, в главном потоке (ТЗ 5.3).
// Поэтому здесь нет await: после await код мог бы продолжиться в другом потоке, а Raylib работает только в этом.
while (!Raylib.WindowShouldClose())
{
    var now = Environment.TickCount64;
    HandlePackets();
    CheckConnection(now);

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Ui.Background);
    switch (screen)
    {
        case Screen.Connect: DrawConnect(); break;
        case Screen.Lobby: DrawLobby(now); break;
        case Screen.Room: DrawRoom(); break;
        case Screen.Game: DrawGame(now); break;
    }
    DrawStatus(now);
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
            roomNameInput.Focused = true;
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
                chatInput.Focused = true;
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

void DrawConnect()
{
    Ui.Text("Призрак и охотники", 440, 200, 48);
    Ui.Text($"Сервер: {server}", 440, 270, 20, Ui.Muted);
    Ui.Text("Ник:", 440, 332);
    var waiting = helloSentAt >= 0;
    var enter = nickInput.Draw(new Rectangle(500, 320, 320, 44), "3–16 символов");
    var click = Ui.Button(new Rectangle(500, 380, 320, 48), waiting ? "Подключение…" : "Войти", enabled: !waiting);
    if ((enter || click) && !waiting && nickInput.Text.Trim().Length > 0)
    {
        Connect(nickInput.Text.Trim());
    }
}

void DrawLobby(long now)
{
    if (now - lastRoomsRequest > 3000)
    {
        RequestRooms(); // список комнат сам обновляется раз в 3 с
    }

    Ui.Text("Лобби", 40, 30, 40);
    Ui.Text($"Вы: {nickInput.Text.Trim()} (id {myId})   ·   пинг: {(pingMs < 0 ? "—" : $"{pingMs} мс")}", 40, 85, 20, Ui.Muted);

    Ui.Text("Комнаты — клик, чтобы войти:", 40, 140, 24);
    if (rooms.Count == 0)
    {
        Ui.Text("Комнат пока нет — создайте свою справа", 40, 185, 20, Ui.Muted);
    }
    for (var i = 0; i < rooms.Count; i++)
    {
        var r = rooms[i];
        var full = r.Players >= r.MaxPlayers;
        var note = r.InGame ? "   — идёт игра" : full ? "   — заполнена" : "";
        if (Ui.Button(new Rectangle(40, 180 + i * 56, 640, 46), $"{r.Name}   {r.Players}/{r.MaxPlayers}{note}", 22, !r.InGame && !full))
        {
            Send(PacketType.JoinRoom, Packets.WriteJoinRoom(r.RoomId));
        }
    }
    if (Ui.Button(new Rectangle(40, 650, 200, 46), "Обновить"))
    {
        RequestRooms();
    }

    Ui.Text("Новая комната:", 760, 140, 24);
    var enter = roomNameInput.Draw(new Rectangle(760, 180, 440, 44), "название, 1–24 символа");
    var click = Ui.Button(new Rectangle(760, 236, 200, 46), "Создать");
    if ((enter || click) && roomNameInput.Text.Trim().Length > 0)
    {
        Send(PacketType.CreateRoom, Packets.WriteCreateRoom(roomNameInput.Text.Trim()));
        roomNameInput.Text = "";
    }
}

void DrawRoom()
{
    if (room is not { } r)
    {
        return;
    }

    Ui.Text($"Комната «{r.Name}»", 40, 30, 40);
    Ui.Text("Раунд начнётся, когда в комнате 3–5 игроков и все готовы", 40, 85, 20, Ui.Muted);

    for (var i = 0; i < r.Players.Count; i++)
    {
        var p = r.Players[i];
        var y = 150 + i * 44;
        var tags = (p.PlayerId == r.HostId ? "  (хост)" : "") + (p.PlayerId == myId ? "  (вы)" : "");
        Ui.Text(p.Nick + tags, 60, y, 24);
        Ui.Text(p.Ready ? "готов" : "не готов", 400, y, 24, p.Ready ? Ui.Accent : Ui.Muted);
    }

    // готовность берём из RoomState — сервер главный (ТЗ 3.4: SetReady переключает)
    var meReady = r.Players.FirstOrDefault(p => p.PlayerId == myId)?.Ready ?? false;
    if (Ui.Button(new Rectangle(40, 400, 220, 50), meReady ? "Не готов" : "Готов"))
    {
        Send(PacketType.SetReady, Array.Empty<byte>());
    }
    if (Ui.Button(new Rectangle(280, 400, 220, 50), "Выйти"))
    {
        // выходящему сервер RoomState не присылает — он уже не в комнате, поэтому уходим в лобби сами
        Send(PacketType.LeaveRoom, Array.Empty<byte>());
        room = null;
        chat.Clear();
        screen = Screen.Lobby;
        roomNameInput.Focused = true;
        RequestRooms();
    }

    Ui.Text("Чат комнаты", 640, 140, 24);
    Raylib.DrawRectangle(640, 180, 600, 480, Ui.Panel);
    Raylib.BeginScissorMode(640, 180, 600, 480); // длинные строки не вылезают за панель
    var lines = chat.TakeLast(18).ToList();
    for (var i = 0; i < lines.Count; i++)
    {
        Ui.Text(lines[i], 652, 190 + i * 25, 20);
    }
    Raylib.EndScissorMode();

    var enter = chatInput.Draw(new Rectangle(640, 676, 460, 44), "сообщение, Enter — отправить");
    var click = Ui.Button(new Rectangle(1110, 676, 130, 44), "Отправить", 20);
    if ((enter || click) && chatInput.Text.Trim().Length > 0)
    {
        Send(PacketType.Chat, Packets.WriteChat(chatInput.Text.Trim()));
        chatInput.Text = "";
    }
}

void DrawGame(long now)
{
    if (game == null)
    {
        return;
    }

    game.Draw();

    // Input 20 раз в секунду независимо от частоты кадров, inputSeq растёт (ТЗ 5.3); без гарантий
    if (now - lastInputAt >= 50)
    {
        lastInputAt = now;
        SendUnreliable(PacketType.Input, Packets.WriteInput(++inputSeq, game.ReadInput()));
    }

    if (game.TimeLeft == 0)
    {
        Raylib.DrawRectangle(440, 320, 400, 150, new Color(0, 0, 0, 210));
        Ui.Text("Время вышло", 560, 340, 32);
        if (Ui.Button(new Rectangle(540, 400, 200, 46), "В комнату"))
        {
            game = null;
            screen = room != null ? Screen.Room : Screen.Lobby;
        }
    }
}

void DrawStatus(long now)
{
    if (screen == Screen.Game || now > statusUntil)
    {
        return;
    }
    var color = status.StartsWith("Ошибка") || status.StartsWith("Сервер") || status.StartsWith("Соединение")
        ? Ui.ErrorColor
        : Ui.Muted;
    Ui.Text(status, 40, 750, 22, color);
}

enum Screen
{
    Connect,
    Lobby,
    Room,
    Game,
}
