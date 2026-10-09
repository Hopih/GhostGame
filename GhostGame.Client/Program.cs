using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

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

async Task HandleClientAsync(Socket socketClient)
{
    var command = Console.ReadLine();
    if (command == "exit")
    {
        livereload = false;
        return;
    }
    
    var bytesSent = await socketClient.SendAsync(
        new ArraySegment<byte>(Encoding.UTF8.GetBytes("Hello from client!")),
        SocketFlags.None);

    Console.WriteLine($"send {bytesSent}");

    var buffer = new byte[256];
    var result = 0;
    var resultMessage = new StringBuilder();
    do
    {
        result = await socketClient.ReceiveAsync(buffer, SocketFlags.None);
        resultMessage.Append(Encoding.UTF8.GetString(buffer));
        if (result < buffer.Length)
        {
            break;
        }
    } while (result > 0);

    Console.WriteLine(resultMessage.ToString());
    
}


