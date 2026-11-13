using System.Net;
using System.Net.Sockets;
using GhostGame.Client;
using GhostGame.Client.Audio;
using GhostGame.Client.Graphics;
using GhostGame.Client.Screens;
using GhostGame.Shared;
using Raylib_cs;

// Адрес сервера вводится на экране входа. По умолчанию — наш сервер на VPS (Docker);
// аргументы запуска «ip [порт]» только подставляют в поле другой адрес (удобно в Rider: 127.0.0.1 5000).
const string DefaultServer = "45.154.185.149:5000";
const int DefaultPort = 5000;
var serverText = args.Length > 0 ? args[0] + (args.Length > 1 ? $":{args[1]}" : "") : DefaultServer;
var server = new IPEndPoint(IPAddress.Loopback, DefaultPort); // настоящий адрес берётся из поля при нажатии «Войти»
var map = GameMap.Load();

// --- сеть: сокет, канал и очередь входящих создаются заново на каждое подключение ---
Socket? socket = null;
Transport? transport = null;
ReliableChannel? channel = null;
var incoming = System.Threading.Channels.Channel.CreateUnbounded<Packet>(); // приём кладёт сюда, главный поток разбирает
var lastHeard = 0L;        // когда последний раз что-то пришло от сервера (пишет фоновый приём)
var pingMs = -1L;
var channelFailed = false; // надёжный пакет не подтверждён 10 с (ставит фоновая задача повторов)

// --- что сейчас на экране и что клиент знает ---
var screen = Screen.Connect;
var connected = false;      // Welcome получен
var helloSentAt = -1L;      // когда отправили Hello; -1 — Welcome не ждём
var myId = 0;
var status = "";
var statusUntil = 0L;
var rooms = new List<RoomInfo>();
var lastRoomsRequest = 0L;
(int RoomId, string Name, int HostId, List<RoomPlayer> Players)? room = null;
var chat = new List<string>();
GameScreen? game = null;
var roundNicks = new Dictionary<int, string>(); // ники участников раунда на момент GameStart — для экрана итогов
(Role Winner, GameOverReason Reason, List<PlayerRole> Players)? result = null;
uint inputSeq = 0;
var lastInputAt = 0L;
var lastPingAt = 0L;

Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
// Окно развёрнуто на весь экран, но его можно уменьшить и растягивать — экраны раскладываются от размера окна.
Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint);
Raylib.InitWindow(1600, 900, "Призрак и охотники");
Raylib.MaximizeWindow();
Raylib.SetWindowMinSize(1240, 780);
Raylib.SetTargetFPS(60);
Raylib.SetExitKey(KeyboardKey.Null); // Esc не закрывает окно
Assets.Load(); // спрайты и шрифт — только после InitWindow
MenuBackdrop.Init(map);
Sfx.Load();             // звуки — тоже после InitWindow

// Экраны меню: только рисуют и сообщают, что нажали. Что отправить серверу, решает этот файл.
var connectScreen = new ConnectScreen(serverText);
var lobbyScreen = new LobbyScreen();
var roomScreen = new RoomScreen();
var resultsScreen = new ResultsScreen();

// Главный цикл кадров. Всё, что связано с окном, — только здесь, в главном потоке.
// Поэтому здесь нет await: после await код мог бы продолжиться в другом потоке, а Raylib работает только в этом.
while (!Raylib.WindowShouldClose())
{
    var now = Environment.TickCount64;
    Sfx.Update();
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
        case Screen.Results: ResultsFrame(); break;
    }
    StatusFrame(now);
    Raylib.EndDrawing();
}

// окно закрыли: 3 копии Disconnect — хотя бы одна должна дойти
if (connected && transport != null)
{
    for (var i = 0; i < 3; i++)
    {
        transport.SendUnreliableAsync(PacketType.ClientDisconnect, Array.Empty<byte>(), server).Wait();
    }
}
Sfx.Unload();
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
void Connect(string nick, string address)
{
    var endpoint = ParseServer(address);
    if (endpoint == null)
    {
        ShowStatus("Ошибка: не понял адрес сервера, нужно ip:порт");
        Sfx.Play(Sfx.Error, 0.6f);
        return;
    }
    if (channel != null && !endpoint.Equals(server))
    {
        // после неудачной попытки (например, ник занят) поменяли адрес — старое подключение бросаем
        channel = null;
        transport = null;
        socket?.Close();
        socket = null;
    }
    if (channel == null)
    {
        server = endpoint;
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

// «ip», «ip:порт», «имя», «имя:порт» → адрес сервера; не разобрали — null. Порт не указан — 5000.
static IPEndPoint? ParseServer(string text)
{
    var host = text.Trim();
    var port = DefaultPort;
    var colon = host.LastIndexOf(':');
    if (colon >= 0)
    {
        if (!int.TryParse(host[(colon + 1)..], out port) || port is < 1 or > 65535)
        {
            return null;
        }
        host = host[..colon];
    }
    if (host.Length == 0)
    {
        return null;
    }
    if (!IPAddress.TryParse(host, out var ip))
    {
        try
        {
            // сокет у клиента IPv4 — берём IPv4-адрес имени
            ip = Dns.GetHostAddresses(host).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        }
        catch (Exception e) when (e is SocketException or ArgumentException)
        {
            return null;
        }
    }
    return ip == null ? null : new IPEndPoint(ip, port);
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
    result = null;
    rooms.Clear();
    chat.Clear();
    screen = Screen.Connect;
    ShowStatus(reason);
}

// Раз в кадр: Welcome за 5 с, Ping раз в секунду, 5 с тишины от сервера — «Соединение потеряно»
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
            Sfx.Play(Sfx.Error, 0.6f);
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
            Sfx.Play(Sfx.Chat, 0.5f);
            break;

        case PacketType.GameStart:
            var (role, roundSeconds, spawns) = Packets.ReadGameStart(packet.Payload);
            roundNicks = room?.Players.ToDictionary(p => p.PlayerId, p => p.Nick) ?? new Dictionary<int, string>();
            game = new GameScreen(map, myId, role, roundSeconds, spawns, roundNicks);
            inputSeq = 0;
            screen = Screen.Game;
            break;

        case PacketType.GameOver:
            // конец раунда: показываем итоги; Snapshot, если ещё придут, вне экрана игры игнорируются
            result = Packets.ReadGameOver(packet.Payload);
            game = null;
            screen = Screen.Results;
            var won = result.Value.Players.FirstOrDefault(p => p.PlayerId == myId)?.Role == result.Value.Winner;
            Sfx.Play(won ? Sfx.Win : Sfx.Lose, 0.8f);
            break;

        case PacketType.Snapshot:
            // Snapshot без гарантий и может обогнать GameStart — вне экрана игры его просто игнорируем
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
    if (connectScreen.Draw(waiting: helloSentAt >= 0))
    {
        Connect(connectScreen.Nick, connectScreen.Server);
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
            Send(PacketType.SetReady, Array.Empty<byte>()); // сервер переключит и пришлёт RoomState
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

    // Input 20 раз в секунду независимо от частоты кадров, inputSeq растёт; без гарантий
    if (now - lastInputAt >= 50)
    {
        lastInputAt = now;
        SendUnreliable(PacketType.Input, Packets.WriteInput(++inputSeq, game.ReadInput()));
    }

    if (Raylib.IsKeyPressed(KeyboardKey.M)) // в игре полей ввода нет, поэтому M можно отдать под звук
    {
        Sfx.ToggleMute();
    }

    if (game.Draw(pingMs)) // время вышло и нажали «В комнату»
    {
        game = null;
        screen = room != null ? Screen.Room : Screen.Lobby;
    }
}

void ResultsFrame()
{
    if (result is not { } r)
    {
        return;
    }

    if (resultsScreen.Draw(r.Winner, r.Reason, r.Players, roundNicks, myId))
    {
        result = null;
        screen = room != null ? Screen.Room : Screen.Lobby;
        if (room != null)
        {
            roomScreen.Focus();
        }
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
    Results,
}
