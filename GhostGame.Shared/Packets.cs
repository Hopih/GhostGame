namespace GhostGame.Shared;

public static class Packets
{
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

    // Ping: sentAt: long — время отправки у клиента, мс
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

    // Pong: sentAt — копия из Ping как есть
    public static byte[] WritePong(long sentAt)
    {
        return WritePing(sentAt);
    }

    public static long ReadPong(byte[] payload)
    {
        return ReadPing(payload);
    }

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

    // SetReady полей не имеет: сервер сам переключает готовность

    // RoomList: список из (roomId: int, name: string, players: byte, maxPlayers: byte, inGame: bool)
    public static byte[] WriteRoomList(List<RoomInfo> rooms)
    {
        var w = new PacketWriter();
        w.WriteByte((byte)rooms.Count);
        foreach (var room in rooms)
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

    // RoomState: roomId: int, name: string, hostId: int, список из (playerId: int, nick: string, ready: bool)
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

    // GameStart: role: byte, roundSeconds: ushort, список из (playerId: int, x: float, y: float)
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

    // GameOver: winner: byte (0 — охотники, 1 — призрак), reason: byte, список из (playerId: int, role: byte)
    public static byte[] WriteGameOver(Role winner, GameOverReason reason, List<PlayerRole> players)
    {
        var w = new PacketWriter();
        w.WriteByte((byte)winner);
        w.WriteByte((byte)reason);
        w.WriteByte((byte)players.Count);
        foreach (var player in players)
        {
            w.WriteInt(player.PlayerId);
            w.WriteByte((byte)player.Role);
        }

        return w.ToArray();
    }

    public static (Role Winner, GameOverReason Reason, List<PlayerRole> Players) ReadGameOver(byte[] payload)
    {
        var r = new PacketReader(payload);
        var winner = (Role)r.ReadByte();
        var reason = (GameOverReason)r.ReadByte();
        var count = r.ReadByte();
        var players = new List<PlayerRole>();
        for (var i = 0; i < count; i++)
        {
            var playerId = r.ReadInt();
            var role = (Role)r.ReadByte();
            players.Add(new PlayerRole(playerId, role));
        }

        r.EnsureEnd();
        return (winner, reason, players);
    }

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

    // Input: inputSeq: uint, moveX: sbyte, moveY: sbyte, aimX: float, aimY: float, actions: byte — 15 байт
    public static byte[] WriteInput(uint inputSeq, InputState input)
    {
        var w = new PacketWriter();
        w.WriteUInt(inputSeq);
        w.WriteSByte(input.MoveX);
        w.WriteSByte(input.MoveY);
        w.WriteFloat(input.AimX);
        w.WriteFloat(input.AimY);
        w.WriteByte(input.Actions);
        return w.ToArray();
    }

    public static (uint InputSeq, InputState Input) ReadInput(byte[] payload)
    {
        var r = new PacketReader(payload);
        var inputSeq = r.ReadUInt();
        var moveX = r.ReadSByte();
        var moveY = r.ReadSByte();
        var aimX = r.ReadFloat();
        var aimY = r.ReadFloat();
        var actions = r.ReadByte();
        r.EnsureEnd();
        return (inputSeq, new InputState(moveX, moveY, aimX, aimY, actions));
    }

    // Snapshot: tick: uint, timeLeft: ushort, игроки по 22 байта (playerId: int, x, y, aimX, aimY: float, flags, charge: byte),
    // curseMask: byte, catchProgress: byte, прогресс проклятия 5 × byte. Число игроков — байт 13 датаграммы
    // (заголовок 7 + tick 4 + timeLeft 2), поэтому новые поля добавляются только в конец.
    public static byte[] WriteSnapshot(Snapshot snapshot)
    {
        var w = new PacketWriter();
        w.WriteUInt(snapshot.Tick);
        w.WriteUShort(snapshot.TimeLeft);
        w.WriteByte((byte)snapshot.Players.Count);
        foreach (var p in snapshot.Players)
        {
            w.WriteInt(p.PlayerId);
            w.WriteFloat(p.X);
            w.WriteFloat(p.Y);
            w.WriteFloat(p.AimX);
            w.WriteFloat(p.AimY);
            w.WriteByte(p.Flags); 
            w.WriteByte(p.Charge);
        }
        w.WriteByte(snapshot.CurseMask);
        w.WriteByte(snapshot.CatchProgress);
        for (var i = 0; i < Snapshot.ItemCount; i++)
        {
            w.WriteByte(snapshot.CurseProgressOf(i));
        }
        return w.ToArray();
    }

    public static Snapshot ReadSnapshot(byte[] payload)
    {
        var r = new PacketReader(payload);
        var tick = r.ReadUInt();
        var timeLeft = r.ReadUShort();
        var count = r.ReadByte();
        var players = new List<SnapshotPlayer>();
        for (var i = 0; i < count; i++)
        {
            var playerId = r.ReadInt();
            var x = r.ReadFloat();
            var y = r.ReadFloat();
            var aimX = r.ReadFloat();
            var aimY = r.ReadFloat();
            var flags = r.ReadByte();
            var charge = r.ReadByte();
            players.Add(new SnapshotPlayer(playerId, x, y, aimX, aimY, flags , charge));
        }
        var curseMask = r.ReadByte();
        var catchProgress = r.ReadByte();
        var curseProgress = new byte[Snapshot.ItemCount];
        for (var i = 0; i < curseProgress.Length; i++)
        {
            curseProgress[i] = r.ReadByte();
        }
        r.EnsureEnd();
        return new Snapshot(tick, timeLeft, players, curseMask, catchProgress, curseProgress);
    }
}    
