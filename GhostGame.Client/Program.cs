using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using GhostGame.Shared;

const PacketType EchoType = PacketType.Chat;

var server = new IPEndPoint(
    args.Length > 0 ? IPAddress.Parse(args[0]) : IPAddress.Loopback,
    args.Length > 1 ? int.Parse(args[1]) : 5000);

var socketClient = new Socket(
    AddressFamily.InterNetwork,
    SocketType.Dgram,
    ProtocolType.Udp);

socketClient.Bind(new IPEndPoint(IPAddress.Any, 0));

var buffer = new byte[PacketIO.ReceiveBufferSize];
bool livereload = true;

while (true)
{
    Console.Write("Ник: ");
    var nick = Console.ReadLine() ?? "";
    await PacketIO.SendPacketAsync(socketClient, server, PacketType.Hello, 0, Packets.WriteHello(1, nick));

    using var helloTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    try
    {
        var (reply, _) = await PacketIO.ReceivePacketAsync(socketClient, buffer, helloTimeout.Token);
        if (reply == null)
        {
            Console.WriteLine("broken datagram from server");
            continue;
        }

        if (reply.Type == PacketType.Welcome)
        {
            Console.WriteLine($"Подключено, ваш id: {Packets.ReadWelcome(reply.Payload)}");
            break;
        }

        if (reply.Type == PacketType.Error)
        {
            var (code, message) = Packets.ReadError(reply.Payload);
            Console.WriteLine($"Ошибка {code}: {message}");
            continue;
        }

        Console.WriteLine($"unexpected packet from server: {reply.Type}");
    }
    catch (MalformedPacketException e)
    {
        Console.WriteLine($"broken datagram from server: {e.Message}");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Сервер не отвечает");
        return;
    }
    catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset)
    {
        Console.WriteLine("Сервер не отвечает");
        return;
    }
}

while (livereload)
{
    await HandleClientAsync(socketClient);
}

socketClient.Close();

async Task HandleClientAsync(Socket socketClient)
{
    var command = Console.ReadLine() ?? "exit";

    if (command == "exit")
    {
        await PacketIO.SendPacketAsync(socketClient, server, EchoType, 0, Packets.WriteChat(command));
        livereload = false;
        return;
    }

    var bytesSent = await PacketIO.SendPacketAsync(socketClient, server, EchoType, 0, Packets.WriteChat(command));

    Console.WriteLine($"send {bytesSent}");

    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
    try
    {
        var (packet, _) = await PacketIO.ReceivePacketAsync(socketClient, buffer, timeout.Token);
        if (packet == null)
        {
            Console.WriteLine("broken datagram from server");
            return;
        }
        
        if (packet.Type == PacketType.Error)
        {
            var (code, message) = Packets.ReadError(packet.Payload);
            Console.WriteLine($"Ошибка {code}: {message}");
            livereload = false;
            return;
        }

        if (packet.Type == PacketType.ServerDisconnect)
        {
            Console.WriteLine($"Сервер отключил вас: {Packets.ReadServerDisconnect(packet.Payload)}");
            livereload = false;
            return;
        }

        Console.WriteLine(Packets.ReadChat(packet.Payload));
    }
    catch (MalformedPacketException e)
    {
        Console.WriteLine($"broken datagram from server: {e.Message}");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("no reply in 2 s (datagram lost?)");
    }
    catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset)
    {
        Console.WriteLine($"server {server} is not running");
    }
}

