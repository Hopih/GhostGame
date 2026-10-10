namespace GhostGame.Shared;
using System;
using System.Net.Sockets;
using System.Buffers.Binary;

public static class PacketIO
{
    private const int MaxPacketSize = 1024 * 1024;
    
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

    public static async Task<int> SendPacketAsync(Socket socket, byte[] data)
    {
        var packet = new byte[data.Length + 4];
        BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(0, 4), data.Length);
        data.CopyTo(packet.AsSpan(4));
        return await socket.SendAsync(packet, SocketFlags.None);
    }

    public static async Task<byte[]?> ReceivePacketAsync(Socket socket)
    {
        var header = ReceiveExactlyAsync(socket, 4).Result;
        if (header is null)
        {
            return null;
        }
        var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4));
        if (length > MaxPacketSize || length < 0)
        {
            return null;
        }
        var body = await ReceiveExactlyAsync(socket, length);
        return body;
    }
    
}