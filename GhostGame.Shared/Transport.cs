using System.Net;
using System.Net.Sockets;

namespace GhostGame.Shared;

public sealed class Transport
{
    private readonly Socket socket;

    public Transport(Socket socket)
    {
        this.socket = socket;
    }

    public async Task SendAsync(byte[] datagram, EndPoint to)
    {
        await socket.SendToAsync(datagram, SocketFlags.None, to);
    }

    // Пакет без гарантий: seq = 0, мимо надёжного канала
    public Task SendUnreliableAsync(PacketType type, byte[] payload, EndPoint to)
    {
        return SendAsync(PacketIO.Build(type, 0, payload), to);
    }
}
