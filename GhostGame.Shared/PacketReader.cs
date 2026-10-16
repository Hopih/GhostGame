using System.Buffers.Binary;
using System.Text;

namespace GhostGame.Shared;

public class MalformedPacketException : Exception
{
    public MalformedPacketException(string message) : base(message) { }
}

public class PacketReader
{
    private readonly byte[] data;
    private int position;

    public PacketReader(byte[] data)
    {
        // запомнить data
        this.data = data;
        position = 0;
    }

    public byte ReadByte()
    {
        EnsureAvailable(1);
        byte value = data[position];
        position += 1;
        return value;
    }

    public sbyte ReadSByte()
    {
        return (sbyte)ReadByte();
    }

    public bool ReadBool()
    {
        byte value = ReadByte();
        if (value > 1)
        {
            throw new MalformedPacketException($"Bool must be 0 or 1, got {value}");
        }
        return value == 1;
    }

    public ushort ReadUShort()
    {
        EnsureAvailable(2);
        ushort value = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position, 2));
        position += 2;
        return value;
    }

    public int ReadInt()
    {
        EnsureAvailable(4);
        int value = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(position, 4));
        position += 4;
        return value;
    }

    public uint ReadUInt()
    {
        EnsureAvailable(4);
        uint value = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position, 4));
        position += 4;
        return value;
    }

    public float ReadFloat()
    {
        EnsureAvailable(4);
        float value = BinaryPrimitives.ReadSingleBigEndian(data.AsSpan(position, 4));
        position += 4;
        return value;
    }

    public long ReadLong()
    {
        EnsureAvailable(8);
        long value = BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(position, 8));
        position += 8;
        return value;
    }

    public string ReadString()
    {
        ushort length = ReadUShort();
        EnsureAvailable(length);
        var str = Encoding.UTF8.GetString(data.AsSpan(position, length));
        position += length;
        return str;
    }

    public void EnsureEnd()
    {
        if (position != data.Length)
        {
            throw new MalformedPacketException("Packet is too long");
        }
    }

    private void EnsureAvailable(int size)
    {
        if (position + size > data.Length)
        {
            throw new MalformedPacketException("Packet is too short");
        }
    }
}