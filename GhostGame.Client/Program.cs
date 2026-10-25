using System.Net;
using System.Net.Sockets;
using System.Text;
using GhostGame.Shared;

var server = new IPEndPoint(
    args.Length > 0 ? IPAddress.Parse(args[0]) : IPAddress.Loopback,
    args.Length > 1 ? int.Parse(args[1]) : 5000);

var socketClient = new Socket(
    AddressFamily.InterNetwork,
    SocketType.Dgram,
    ProtocolType.Udp);

socketClient.Bind(new IPEndPoint(IPAddress.Any, 0));

var transport = new Transport(socketClient);
var channel = new ReliableChannel(transport, server);
// приём идёт в фоне и складывает готовые пакеты сюда, главный поток их разбирает (ТЗ 5.3)
var incoming = System.Threading.Channels.Channel.CreateUnbounded<Packet>();
var myId = 0;
var myready = false;
var lastHeard = Environment.TickCount64; // когда последний раз что-то пришло от сервера
var pingMs = -1L;

_ = Task.Run(ReceiveLoopAsync);
_ = Task.Run(ResendLoopAsync);

// Рукопожатие: Hello → Welcome или Error, ждём не больше 5 секунд (ТЗ 5.3)
while (true)
{
    Console.Write("Ник: ");
    var nick = Console.ReadLine() ?? "";
    await channel.SendAsync(PacketType.Hello, Packets.WriteHello(1, nick));

    var reply = await WaitForReplyAsync(TimeSpan.FromSeconds(5));
    if (reply == null)
    {
        Console.WriteLine("Сервер не отвечает");
        return;
    }

    try
    {
        if (reply.Type == PacketType.Welcome)
        {
            myId = Packets.ReadWelcome(reply.Payload);
            Console.WriteLine($"Подключено, ваш id: {myId}");
            break;
        }

        var (code, message) = Packets.ReadError(reply.Payload);
        Console.WriteLine($"Ошибка {code}: {message}");
    }
    catch (MalformedPacketException e)
    {
        Console.WriteLine($"broken datagram from server: {e.Message}");
    }
}

_ = Task.Run(PrintLoopAsync);
_ = Task.Run(PingLoopAsync);
Console.WriteLine("Пишите сообщения. /flood N — отправить N пронумерованных сообщений, /ping — пинг, exit — выход.");

while (true)
{
    var line = Console.ReadLine();
    if (line == null || line == "exit")
    {
        break;
    }

    if (line == "/ping")
    {
        Console.WriteLine(pingMs < 0 ? "пинг ещё не измерен" : $"пинг: {pingMs} мс");
        continue;
    }

    if (line.StartsWith("/flood ") && int.TryParse(line[7..], out var count))
    {
        for (var i = 1; i <= count; i++)
        {
            await channel.SendAsync(PacketType.Chat, Packets.WriteChat($"msg {i}"));
        }
        continue;
    }

    if (line == "/rooms")
    {
        await channel.SendAsync(PacketType.ListRooms, Array.Empty<byte>());
        continue;
    }

    if (line.StartsWith("/create"))
    {
        var roomName = line.Split(' ')[1];
        await channel.SendAsync(PacketType.CreateRoom, Packets.WriteCreateRoom(roomName));
        continue;
    }

    if (line.StartsWith("/join"))
    {
        var roomId = int.Parse(line.Split(' ')[1]);
        await channel.SendAsync(PacketType.JoinRoom, Packets.WriteJoinRoom(roomId));
        continue;   
    }

    if (line.StartsWith("/ready"))
    {
        await channel.SendAsync(PacketType.SetReady, Packets.WriteSetReady(!myready));
        continue;  
    }

    if (line.StartsWith("/leave"))
    {
        await channel.SendAsync(PacketType.LeaveRoom, Array.Empty<byte>());
        continue; 
    }

    if (line.Length > 0)
    {
        await channel.SendAsync(PacketType.Chat, Packets.WriteChat(line));
    }
}

// перед выходом даём неподтверждённым сообщениям дойти (не дольше 3 с)
for (var waited = 0; channel.PendingCount > 0 && waited < 3000; waited += 50)
{
    await Task.Delay(50);
}

// при закрытии 3 копии Disconnect: хотя бы одна должна дойти (ТЗ 3.7)
for (var i = 0; i < 3; i++)
{
    await transport.SendUnreliableAsync(PacketType.ClientDisconnect, Array.Empty<byte>(), server);
}
socketClient.Close();

async Task<Packet?> WaitForReplyAsync(TimeSpan timeout)
{
    using var cts = new CancellationTokenSource(timeout);
    try
    {
        while (true)
        {
            var packet = await incoming.Reader.ReadAsync(cts.Token);
            if (packet.Type is PacketType.Welcome or PacketType.Error)
            {
                return packet;
            }
        }
    }
    catch (OperationCanceledException)
    {
        return null;
    }
}

async Task ReceiveLoopAsync()
{
    var buffer = new byte[PacketIO.ReceiveBufferSize];
    while (true)
    {
        try
        {
            var (packet, from) = await PacketIO.ReceivePacketAsync(socketClient, buffer);
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

            foreach (var ready in await channel.ReceiveAsync(packet))
            {
                incoming.Writer.TryWrite(ready);
            }
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset)
        {
            // Windows так сообщает, что на порту сервера никто не слушает; рукопожатие скажет «Сервер не отвечает»
        }
        catch (MalformedPacketException e)
        {
            Console.WriteLine($"broken datagram from server: {e.Message}");
        }
        catch (ObjectDisposedException)
        {
            return;
        }
    }
}

async Task ResendLoopAsync()
{
    while (true)
    {
        await Task.Delay(50);
        var (_, failed) = await channel.ResendAsync();
        if (failed)
        {
            Console.WriteLine("Соединение потеряно: сервер не подтверждает пакеты");
            Environment.Exit(1);
        }
    }
}

// Ping раз в секунду (ТЗ 3.4); нет ни одной датаграммы от сервера 5 с — соединение потеряно (ТЗ 5.3)
async Task PingLoopAsync()
{
    while (true)
    {
        await transport.SendUnreliableAsync(PacketType.Ping, Packets.WritePing(Environment.TickCount64), server);
        await Task.Delay(1000);
        if (Environment.TickCount64 - lastHeard > 5000)
        {
            Console.WriteLine("Соединение потеряно");
            Environment.Exit(1);
        }
    }
}

async Task PrintLoopAsync()
{
    await foreach (var packet in incoming.Reader.ReadAllAsync())
    {
        try
        {
            switch (packet.Type)
            {
                case PacketType.ChatMessage:
                    var (playerId, text) = Packets.ReadChatMessage(packet.Payload);
                    Console.WriteLine(playerId == myId ? $"[я] {text}" : $"[{playerId}] {text}");
                    break;

                case PacketType.Error:
                    var (code, message) = Packets.ReadError(packet.Payload);
                    Console.WriteLine($"Ошибка {code}: {message}");
                    break;

                case PacketType.ServerDisconnect:
                    Console.WriteLine($"Сервер отключил вас: {Packets.ReadServerDisconnect(packet.Payload)}");
                    Environment.Exit(0);
                    break;
            }
        }
        catch (MalformedPacketException e)
        {
            Console.WriteLine($"broken datagram from server: {e.Message}");
        }
    }
}
