using GhostGame.Shared;

namespace GhostGame.Server;

// Комната: свои игроки и всё, что с ними происходит — вход, выход, хост, готовность, рассылки, старт раунда
public class Room
{
    public const int MaxPlayers = 5;
    public const int MinPlayersToStart = 3;
    public const ushort RoundSeconds = 240;

    private static int id = 1;

    public Room(string name)
    {
        Name = name;
        Id = id++;
    }

    public int Id { get; }
    public string Name { get; }
    public int HostId { get; private set; }
    public bool InGame { get; private set; }

    // игроки комнаты: ключ — playerId
    public Dictionary<int, ClientSession> Players { get; } = new();

    // состояния игроков в текущем раунде: ключ — playerId; пусто, пока раунд не идёт
    public Dictionary<int, PlayerState> Round { get; } = new();

    public bool IsFull => Players.Count >= MaxPlayers;
    public bool IsEmpty => Players.Count == 0;
    public bool CanStart => !InGame && Players.Count >= MinPlayersToStart && Players.Values.All(p => p.IsReady);

    public void Add(ClientSession player)
    {
        Players[player.PlayerId] = player;
        player.RoomId = Id;
        player.IsReady = false;
        if (Players.Count == 1)
        {
            HostId = player.PlayerId; // первый вошедший (создатель) — хост
        }
    }

    public void Remove(ClientSession player)
    {
        Players.Remove(player.PlayerId);
        player.RoomId = -1;
        player.IsReady = false;
        if (player.PlayerId == HostId && !IsEmpty)
        {
            HostId = Players.Keys.First(); // хост ушёл — хостом становится следующий
        }
    }

    // «Табличка на двери» для RoomList
    public RoomInfo ToInfo()
    {
        return new RoomInfo(Id, Name, (byte)Players.Count, MaxPlayers, InGame);
    }

    // Надёжный пакет всем игрокам комнаты
    public async Task BroadcastAsync(PacketType type, byte[] payload)
    {
        foreach (var player in Players.Values)
        {
            await player.Channel.SendAsync(type, payload);
        }
    }

    // RoomState всем: шлётся при любом изменении комнаты (ТЗ 3.5)
    public Task BroadcastStateAsync()
    {
        var list = Players.Values.Select(p => new RoomPlayer(p.PlayerId, p.Nick!, p.IsReady)).ToList();
        return BroadcastAsync(PacketType.RoomState, Packets.WriteRoomState(Id, Name, HostId, list));
    }

    // Старт раунда: случайный призрак, каждому свой GameStart со своей ролью (ТЗ 4.2)
    public async Task<ClientSession> StartGameAsync(GameMap map)
    {
        InGame = true;
        var players = Players.Values.ToList();
        var ghost = players[Random.Shared.Next(players.Count)];
        // точки появления пока (0, 0) — настоящие будут на этапе 5 вместе с картой
        var ghostPosition = players.Where(p => p.PlayerId == ghost.PlayerId)
            .Select(p => new PlayerSpawn(p.PlayerId,map.GhostSpawn.X, map.GhostSpawn.Y)).ToList();
        
        var huntersPosition = players.Where(p => p.PlayerId != ghost.PlayerId).Select((p,i )
            => new PlayerSpawn(p.PlayerId, map.HunterSpawns[i].X , map.HunterSpawns[i].Y )).ToList();
        
        var spawns = ghostPosition.Concat(huntersPosition).ToList();
        
        Round.Clear();
        foreach (var s in spawns)
        {
            var role = s.PlayerId == ghost.PlayerId ? Role.Ghost : Role.Hunter;
            Round[s.PlayerId] = new PlayerState(Players[s.PlayerId], role, s.X, s.Y);
        }
        
        foreach (var player in players)
        {
            var role = player == ghost ? Role.Ghost : Role.Hunter;
            await player.Channel.SendAsync(PacketType.GameStart, Packets.WriteGameStart(role, RoundSeconds, spawns));
        }
        return ghost;
    }
}
