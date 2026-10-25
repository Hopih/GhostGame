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

    // ListRooms и LeaveRoom полей не имеют: данные пакета — Array.Empty<byte>()

    // CreateRoom: name: string (ТЗ 3.4)
    public static byte[] WriteCreateRoom(string name)
    {
        var w = new PacketWriter();
        w.WriteString(name);
        return w.ToArray();
    }

    public static string ReadCreateRoom(byte[] payload)
    {
        var r = new PacketReader(payload);
        var name = r.ReadString();
        r.EnsureEnd();
        return name;
    }

    // JoinRoom: roomId: int (ТЗ 3.4)
    public static byte[] WriteJoinRoom(int roomId)
    {
        var w = new PacketWriter();
        w.WriteInt(roomId);
        return w.ToArray();
    }

    public static int ReadJoinRoom(byte[] payload)
    {
        var r = new PacketReader(payload);
        var roomId = r.ReadInt();
        r.EnsureEnd();
        return roomId;
    }

    // SetReady: ready: bool (ТЗ 3.4)
    public static byte[] WriteSetReady(bool ready)
    {
        var w = new PacketWriter();
        w.WriteBool(ready);
        return w.ToArray();
    }

    public static bool ReadSetReady(byte[] payload)
    {
        var r = new PacketReader(payload);
        var ready = r.ReadBool();
        r.EnsureEnd();
        return ready;
    }

    // RoomList: список из (roomId: int, name: string, players: byte, maxPlayers: byte, inGame: bool) (ТЗ 3.5)
    public static byte[] WriteRoomList(List<RoomInfo> rooms)
    {
        var w = new PacketWriter();
        w.WriteByte((byte)rooms.Count);          // список: сначала количество
        foreach (var room in rooms)              // потом элементы подряд
        {
            w.WriteInt(room.RoomId);
            w.WriteString(room.Name);
            w.WriteByte(room.Players);
            w.WriteByte(room.MaxPlayers);
            w.WriteBool(room.InGame);
        }
        return w.ToArray();
    }

    public static List<RoomInfo> ReadRoomList(byte[] payload)
    {
        var r = new PacketReader(payload);
        var count = r.ReadByte();
        var rooms = new List<RoomInfo>();
        for (var i = 0; i < count; i++)
        {
            var roomId = r.ReadInt();
            var name = r.ReadString();
            var players = r.ReadByte();
            var maxPlayers = r.ReadByte();
            var inGame = r.ReadBool();
            rooms.Add(new RoomInfo(roomId, name, players, maxPlayers, inGame));
        }
        r.EnsureEnd();
        return rooms;
    }

    // RoomState: roomId: int, name: string, hostId: int, список из (playerId: int, nick: string, ready: bool) (ТЗ 3.5)
    public static byte[] WriteRoomState(int roomId, string name, int hostId, List<RoomPlayer> players)
    {
        var w = new PacketWriter();
        w.WriteInt(roomId);
        w.WriteString(name);
        w.WriteInt(hostId);
        w.WriteByte((byte)players.Count);
        foreach (var player in players)
        {
            w.WriteInt(player.PlayerId);
            w.WriteString(player.Nick);
            w.WriteBool(player.Ready);
        }
        return w.ToArray();
    }

    public static (int RoomId, string Name, int HostId, List<RoomPlayer> Players) ReadRoomState(byte[] payload)
    {
        var r = new PacketReader(payload);
        var roomId = r.ReadInt();
        var name = r.ReadString();
        var hostId = r.ReadInt();
        var count = r.ReadByte();
        var players = new List<RoomPlayer>();
        for (var i = 0; i < count; i++)
        {
            var playerId = r.ReadInt();
            var nick = r.ReadString();
            var ready = r.ReadBool();
            players.Add(new RoomPlayer(playerId, nick, ready));
        }
        r.EnsureEnd();
        return (roomId, name, hostId, players);
    }

    // GameStart: role: byte, roundSeconds: ushort, список из (playerId: int, x: float, y: float) (ТЗ 3.5)
    public static byte[] WriteGameStart(Role role, ushort roundSeconds, List<PlayerSpawn> spawns)
    {
        var w = new PacketWriter();
        w.WriteByte((byte)role);
        w.WriteUShort(roundSeconds);
        w.WriteByte((byte)spawns.Count);
        foreach (var spawn in spawns)
        {
            w.WriteInt(spawn.PlayerId);
            w.WriteFloat(spawn.X);
            w.WriteFloat(spawn.Y);
        }
        return w.ToArray();
    }

    public static (Role Role, ushort RoundSeconds, List<PlayerSpawn> Spawns) ReadGameStart(byte[] payload)
    {
        var r = new PacketReader(payload);
        var role = (Role)r.ReadByte();
        var roundSeconds = r.ReadUShort();
        var count = r.ReadByte();
        var spawns = new List<PlayerSpawn>();
        for (var i = 0; i < count; i++)
        {
            var playerId = r.ReadInt();
            var x = r.ReadFloat();
            var y = r.ReadFloat();
            spawns.Add(new PlayerSpawn(playerId, x, y));
        }
        r.EnsureEnd();
        return (role, roundSeconds, spawns);
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
