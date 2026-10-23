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

    // Ping: sentAt: long, мс (ТЗ 3.4)
    public static byte[] WritePing(long sentAt)
    {
        var w = new PacketWriter();
        w.WriteLong(sentAt);
        return w.ToArray();
    }

    public static long ReadPing(byte[] payload)
    {
        var r = new PacketReader(payload);
        var sentAt = r.ReadLong();
        r.EnsureEnd();
        return sentAt;
    }

    // Pong: sentAt: long, копия из Ping как есть (ТЗ 3.5)
    public static byte[] WritePong(long sentAt)
    {
        return WritePing(sentAt);
    }

    public static long ReadPong(byte[] payload)
    {
        return ReadPing(payload);
    }

    // Ack: ackSeq: uint (ТЗ 3.4, 3.5 — в обе стороны)
    public static byte[] WriteAck(uint ackSeq)
    {
        var w = new PacketWriter();
        w.WriteUInt(ackSeq);
        return w.ToArray();
    }

    public static uint ReadAck(byte[] payload)
    {
        var r = new PacketReader(payload);
        var ackSeq = r.ReadUInt();
        r.EnsureEnd();
        return ackSeq;
    }

    // ChatMessage: playerId: int, text: string (ТЗ 3.5)
    public static byte[] WriteChatMessage(int playerId, string text)
    {
        var w = new PacketWriter();
        w.WriteInt(playerId);
        w.WriteString(text);
        return w.ToArray();
    }

    public static (int PlayerId, string Text) ReadChatMessage(byte[] payload)
    {
        var r = new PacketReader(payload);
        var playerId = r.ReadInt();
        var text = r.ReadString();
        r.EnsureEnd();
        return (playerId, text);
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

    // ServerDisconnect: reason: byte (ТЗ 3.5)
    public static byte[] WriteServerDisconnect(DisconnectReason reason)
    {
        var w = new PacketWriter();
        w.WriteByte((byte)reason);
        return w.ToArray();
    }

    public static DisconnectReason ReadServerDisconnect(byte[] payload)
    {
        var r = new PacketReader(payload);
        var reason = (DisconnectReason)r.ReadByte();
        r.EnsureEnd();
        return reason;
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
