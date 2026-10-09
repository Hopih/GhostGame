using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

var socketServer = new Socket(
    AddressFamily.InterNetwork,
    SocketType.Stream,
    ProtocolType.Tcp);

socketServer.Bind(
    new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), 5000));
socketServer.Listen();

while (true)
{
    var connectionSocket = await socketServer.AcceptAsync();
    _ = HandeClientAsync(connectionSocket);
}

async Task HandeClientAsync(Socket connectionSocket)
{
    Console.WriteLine("Client connected!");
    var buffer = new byte[1024];
    var receive = new StringBuilder();
    var result = 0;

    do
    {
        result = await connectionSocket.ReceiveAsync(buffer, SocketFlags.None);
        if (result == 0)
        {
            break;
        }
        receive.Append(Encoding.UTF8.GetString(buffer));
        Console.WriteLine(receive.ToString());
        receive.Clear();

        var bytesSent = await connectionSocket.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes("Connection established!")),
            SocketFlags.None);
    } while (true);
    
}






