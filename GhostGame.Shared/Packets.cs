namespace GhostGame.Shared;

public static class Packets
{
    // Hello: version: byte, nick: string (ТЗ 3.4)
    public static byte[] WriteHello(byte version, string nick)
    {
        var w = new PacketWriter();
        w.WriteByte(version);
        w.WriteString(nick);
        return w.ToArray();
    }

    public static (byte Version, string Nick) ReadHello(byte[] payload)
    {
        var r = new PacketReader(payload);
        var version = r.ReadByte();
        var nick = r.ReadString();
        r.EnsureEnd();
        return (version, nick);
    }

    // Chat: text: string (ТЗ 3.4)
    public static byte[] WriteChat(string text)
    {
        var w = new PacketWriter();
        w.WriteString(text);
        return w.ToArray();
    }

    public static string ReadChat(byte[] payload)
    {
        var r = new PacketReader(payload);
        var text = r.ReadString();
        r.EnsureEnd();
        return text;
    }

    // Welcome: playerId: int (ТЗ 3.5)
    public static byte[] WriteWelcome(int playerId)
    {
        var w = new PacketWriter();
        w.WriteInt(playerId);
        return w.ToArray();
    }

    public static int ReadWelcome(byte[] payload)
    {
        var r = new PacketReader(payload);
        var playerId = r.ReadInt();
        r.EnsureEnd();
        return playerId;
    }

    // Error: code: byte, message: string (ТЗ 3.5)
    public static byte[] WriteError(ErrorCode code, string message)
    {
        var w = new PacketWriter();
        w.WriteByte((byte)code);
        w.WriteString(message);
        return w.ToArray();
    }

    public static (ErrorCode Code, string Message) ReadError(byte[] payload)
    {
        var r = new PacketReader(payload);
        var code = (ErrorCode)r.ReadByte();
        var message = r.ReadString();
        r.EnsureEnd();
        return (code, message);
    }
}
