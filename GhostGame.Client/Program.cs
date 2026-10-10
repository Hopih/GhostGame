using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using GhostGame.Shared;

var socketClient = new Socket(
    AddressFamily.InterNetwork, 
    SocketType.Stream, 
    ProtocolType.Tcp);

bool livereload = true;

await socketClient.ConnectAsync("localhost", 5000);

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
    
    var bytesSent = await PacketIO.SendPacketAsync(socketClient, Encoding.UTF8.GetBytes(command));

    Console.WriteLine($"send {bytesSent}");
    
    var result = await PacketIO.ReceivePacketAsync(socketClient);
    if (result == null)
    {
        livereload = false;
        return;
    }
    
    Console.WriteLine(Encoding.UTF8.GetString(result));
}


