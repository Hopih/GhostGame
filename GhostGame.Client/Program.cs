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
bool first_packet = true;

while (livereload)
{
    await HandleClientAsync(socketClient);
}

socketClient.Close();

async Task HandleClientAsync(Socket socketClient)
{   
    var w = new PacketWriter();
    if (first_packet)
    {
        w.WriteString("Клиент подключен");
        await PacketIO.SendPacketAsync(socketClient, server, PacketType.Hello, 0, w.ToArray());
        w.Clear();
        first_packet = false;
    }
    
    var command = Console.ReadLine() ?? "exit";
    w.WriteString(command);
    
    if (command == "exit")
    {
        await PacketIO.SendPacketAsync(socketClient, server, EchoType, 0, w.ToArray());
        livereload = false;
        return;
    }

    var bytesSent = await PacketIO.SendPacketAsync(socketClient, server, EchoType, 0, w.ToArray());

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
        
        var r = new PacketReader(packet.Payload);
        var text = r.ReadString();
        r.EnsureEnd();
        Console.WriteLine(text);

        if (packet.Type == PacketType.Error)
        {
            livereload = false;
        }
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

