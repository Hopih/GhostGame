using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using GhostGame.Shared;

const byte EchoType = 1;

var server = new IPEndPoint(
    args.Length > 0 ? IPAddress.Parse(args[0]) : IPAddress.Loopback,
    args.Length > 1 ? int.Parse(args[1]) : 5000);

var socketClient = new Socket(
    AddressFamily.InterNetwork,
    SocketType.Dgram,
    ProtocolType.Udp);

// port 0 = let the OS pick a free one, so several clients can run on one PC
socketClient.Bind(new IPEndPoint(IPAddress.Any, 0));

var buffer = new byte[PacketIO.ReceiveBufferSize];
bool livereload = true;

while (livereload)
{
    await HandleClientAsync(socketClient);
}

socketClient.Close();

async Task HandleClientAsync(Socket socketClient)
{
    var command = Console.ReadLine();
    if (command == "exit" || command == null)
    {
        livereload = false;
        return;
    }

    var bytesSent = await PacketIO.SendPacketAsync(socketClient, server, EchoType, 0, Encoding.UTF8.GetBytes(command));

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
        Console.WriteLine(Encoding.UTF8.GetString(packet.Payload));
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
