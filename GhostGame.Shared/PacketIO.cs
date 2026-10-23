namespace GhostGame.Shared;
using System;
using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;

public sealed record Packet(PacketType Type, uint Seq, byte[] Payload);

public static class PacketIO
{
    public const ushort Magic = 0x4748;
    public const int HeaderSize = 7;
    public const int MaxDatagramSize = 1200;
    public const int ReceiveBufferSize = 2048;
    
    public static byte[] Build(PacketType type, uint seq, ReadOnlySpan<byte> payload)
    {
        if (HeaderSize + payload.Length > MaxDatagramSize)
        {
            throw new ArgumentException($"Packet is larger than {MaxDatagramSize} bytes", nameof(payload));
        }
        var packet = new byte[HeaderSize + payload.Length];
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(0, 2), Magic);
        packet[2] = (byte)type;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(3, 4), seq);
        payload.CopyTo(packet.AsSpan(HeaderSize));
        return packet;
    }

    public static Packet? Parse(ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length < HeaderSize || BinaryPrimitives.ReadUInt16BigEndian(datagram) != Magic)
        {
            return null;
        }
        var type = (PacketType)datagram[2];
        if (!Enum.IsDefined(type))
        {
            return null;
        }
        var seq = BinaryPrimitives.ReadUInt32BigEndian(datagram.Slice(3, 4));
        return new Packet(type, seq, datagram.Slice(HeaderSize).ToArray());
    }

    public static async Task<(Packet? Packet, IPEndPoint From)> ReceivePacketAsync(
        Socket socket, byte[] buffer, CancellationToken cancellationToken = default)
    {
        var result = await socket.ReceiveFromAsync(
            buffer.AsMemory(), SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), cancellationToken);
        var from = (IPEndPoint)result.RemoteEndPoint;
        return (Parse(buffer.AsSpan(0, result.ReceivedBytes)), from);
    }
    
    public static void DisableConnectionReset(Socket socket)
    {
        if (OperatingSystem.IsWindows())
        {
            const int SioUdpConnReset = -1744830452;
            socket.IOControl((IOControlCode)SioUdpConnReset, new byte[4], null);
        }
    }
}
