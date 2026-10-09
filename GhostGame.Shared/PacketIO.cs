namespace GhostGame.Shared;
using System;
using System.Net.Sockets;

public static class PacketIO
{
    public static async Task<byte[]?> ReceiveExactlyAsync(Socket socket, int count)
    {
        int offset = 0;
        var buffer = new byte[count];
        while (offset < count)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(offset,count - offset), SocketFlags.None);
            if (result == 0)
            {
                return null;
            }
            offset += result;
            
        }
        return buffer;
    }
    
}