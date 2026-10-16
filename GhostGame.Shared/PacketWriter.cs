using System.Text;
using System.Buffers.Binary;

namespace GhostGame.Shared;

public class PacketWriter
{
    private const int MaxPayloadSize = PacketIO.MaxDatagramSize - PacketIO.HeaderSize;

    private List<byte> buffer = new List<byte>();

    public void WriteByte(byte b)
    {
        EnsureSpace(1);
        buffer.Add(b);
    }

    public void WriteSByte(sbyte value)
    {
        WriteByte((byte)value);
    }

    public void WriteBool(bool value)
    {
        WriteByte(value ? (byte)1 : (byte)0);
    }

    public void WriteUShort(ushort value)
    {
        EnsureSpace(2);
        Span<byte> tmp = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(tmp, value);
        buffer.AddRange(tmp);
    }

    public void WriteInt(int value)
    {
        EnsureSpace(4);
        Span<byte> tmp = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(tmp, value);
        buffer.AddRange(tmp);
    }

    public void WriteUInt(uint value)
    {
        EnsureSpace(4);
        Span<byte> tmp = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(tmp, value);
        buffer.AddRange(tmp);
    }

    public void WriteFloat(float value)
    {
        EnsureSpace(4);
        Span<byte> tmp = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(tmp, value);
        buffer.AddRange(tmp);
    }

    public void WriteLong(long value)
    {
        EnsureSpace(8);
        Span<byte> tmp = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(tmp, value);
        buffer.AddRange(tmp);
    }

    public void WriteString(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        EnsureSpace(2 + bytes.Length);
        WriteUShort((ushort)bytes.Length);
        buffer.AddRange(bytes);
    }

    public byte[] ToArray()
    {
        return buffer.ToArray();
    }

    private void EnsureSpace(int size)
    {
        if (buffer.Count + size > MaxPayloadSize)
        {
            throw new InvalidOperationException($"Packet payload is larger than {MaxPayloadSize} bytes");
        }
    }
}
