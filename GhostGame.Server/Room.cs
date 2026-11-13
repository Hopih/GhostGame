using GhostGame.Shared;
using System.Numerics;
namespace GhostGame.Server;

public class Room
{
    public const int MaxPlayers = 5;
    public const int MinPlayersToStart = 3;
    public const ushort RoundSeconds = 240;

    private const float MaxInLight = 3;
    private const float MaxBattery = 6;
    private float InLight = 0;

    private const float CurseSeconds = 3;
    private const float CurseDistance = 1;
    private const int CursesToWin = 3;
    private byte curseMask;
    private readonly float[] curseSeconds = new float[Snapshot.ItemCount];

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

    public Dictionary<int, ClientSession> Players { get; } = new();

    public Dictionary<int, PlayerState> Round { get; } = new();

    // Round трогают два потока: цикл приёма (ApplyInput) и игровой цикл комнаты.
    // Один замок на всю комнату: все, кто читает или пишет Round, запирают именно его.
    private readonly object roundLock = new();

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
            HostId = player.PlayerId;
        }
    }

    public void Remove(ClientSession player)
    {
        Players.Remove(player.PlayerId);
        player.RoomId = -1;
        player.IsReady = false;
        if (player.PlayerId == HostId && !IsEmpty)
        {
            HostId = Players.Keys.First();
        }
    }

    public RoomInfo ToInfo()
    {
        return new RoomInfo(Id, Name, (byte)Players.Count, MaxPlayers, InGame);
    }

    public async Task BroadcastAsync(PacketType type, byte[] payload)
    {
        foreach (var player in Players.Values)
        {
            await player.Channel.SendAsync(type, payload);
        }
    }

    public Task BroadcastStateAsync()
    {
        var list = Players.Values.Select(p => new RoomPlayer(p.PlayerId, p.Nick!, p.IsReady)).ToList();
        return BroadcastAsync(PacketType.RoomState, Packets.WriteRoomState(Id, Name, HostId, list));
    }

    // Старт раунда: случайный призрак, каждому свой GameStart со своей ролью
    public async Task<ClientSession> StartGameAsync(GameMap map , Transport transport)
    {
        InGame = true;
        var players = Players.Values.ToList();
        var ghost = players[Random.Shared.Next(players.Count)];
        var ghostPosition = players.Where(p => p.PlayerId == ghost.PlayerId)
            .Select(p => new PlayerSpawn(p.PlayerId,map.GhostSpawn.X, map.GhostSpawn.Y)).ToList();

        var huntersPosition = players.Where(p => p.PlayerId != ghost.PlayerId).Select((p,i )
            => new PlayerSpawn(p.PlayerId, map.HunterSpawns[i].X , map.HunterSpawns[i].Y )).ToList();

        var spawns = ghostPosition.Concat(huntersPosition).ToList();

        lock (roundLock)
        {
            Round.Clear();
            InLight = 0;
            curseMask = 0;
            Array.Clear(curseSeconds);
            foreach (var s in spawns)
            {
                var role = s.PlayerId == ghost.PlayerId ? Role.Ghost : Role.Hunter;
                Round[s.PlayerId] = new PlayerState(Players[s.PlayerId], role, s.X, s.Y);
            }
        }

        foreach (var player in players)
        {
            var role = player == ghost ? Role.Ghost : Role.Hunter;
            await player.Channel.SendAsync(PacketType.GameStart, Packets.WriteGameStart(role, RoundSeconds, spawns));
        }

        _ = Task.Run((()
            => GameLoopAsync(map, transport)));


        return ghost;
    }

    // Запомнить Input игрока. Номер не больше последнего принятого — опоздавший или повтор, выбрасываем.
    // Пропуск номеров не страшен: новый Input и так говорит, что нажато сейчас.
    public void ApplyInput(int playerId, uint inputSeq, InputState input)
    {
        lock (roundLock)
        {
            if (!InGame || !Round.TryGetValue(playerId, out var player))
            {
                return;
            }

            if (inputSeq <= player.LastInputSeq)
            {
                return;
            }

            player.LastInputSeq = inputSeq;
            player.Input = input;
            player.LastInputAt = Environment.TickCount64;
        }
    }

    // Игровой цикл комнаты, в фоне, пока идёт раунд. Каждые 50 мс под roundLock: сдвинуть всех по Input
    // (500 мс без Input — стоит), посчитать фонари, поимку и проклятия. Потом вне замка разослать Snapshot
    // (охотникам — без призрака, пока он не в луче), а если раунд кончился — GameOver.
    public async Task GameLoopAsync(GameMap map, Transport transport)
    {
        const float TickSeconds = 0.05f;
        const float PlayerRadius = 0.35f;

        uint tick = 0;
        var endAt = Environment.TickCount64 + RoundSeconds * 1000L;

        while (InGame)
        {
            await Task.Delay(50);
            tick++;
            var now = Environment.TickCount64;
            var msLeft = Math.Max(0, endAt - now);
            var timeLeft = (ushort)((msLeft + 999) / 1000);

            var snapshots = new List<SnapshotPlayer>();
            List<PlayerState> recipients;
            List<SnapshotPlayer> huntersView;
            GameOverReason? over = null;
            List<PlayerRole>? gameOverPlayers = null;
            byte catchProgress = 0;
            byte chargeProgress = 0;
            byte[] curseProgress;
            lock (roundLock)
            {
                foreach (var player in Round.Values)
                {
                    var input = now - player.LastInputAt > 500 ? InputState.Idle : player.Input;

                    var step = player.Speed * TickSeconds;
                    if (input.MoveX != 0 && input.MoveY != 0)
                    {
                        step *= 0.7071f;
                    }

                    var newX = player.X + input.MoveX * step;
                    if (map.CanStandAt(newX, player.Y, PlayerRadius))
                    {
                        player.X = newX;
                    }

                    var newY = player.Y + input.MoveY * step;
                    if (map.CanStandAt(player.X, newY, PlayerRadius))
                    {
                        player.Y = newY;
                    }

                    player.PrimaryHeld = input.Primary;
                    player.FlashlightOn = player.Role == Role.Hunter && player.PrimaryHeld && !player.Muted;
                    var flags = player.FlashlightOn ? SnapshotPlayer.FlagFlashlight : (byte)0;

                    if (flags == 1)
                    {
                        if (player.Battery >= MaxBattery)
                        {
                            flags = 0;
                            player.Muted = true;
                        }
                        else
                        {
                            player.Battery += TickSeconds;
                        }
                    }
                    if (player.Muted)
                    {
                        flags = 0;
                        flags |= SnapshotPlayer.FlashlightOverheated;
                        if (player.Battery == 0)
                        {
                            player.Muted = false;
                        }
                    }

                    if ((flags & SnapshotPlayer.FlagFlashlight) == 0)
                    {
                        player.Battery = Math.Max(0, player.Battery - TickSeconds);
                    }

                    chargeProgress = (byte)((MaxBattery - player.Battery) / MaxBattery * 100);

                    snapshots.Add(new SnapshotPlayer(player.Session.PlayerId, player.X, player.Y,
                        player.Input.AimX, player.Input.AimY, flags , chargeProgress));
                }

                var ghost = Round.Values.FirstOrDefault(p => p.Role == Role.Ghost);
                var ghostVisible = ghost != null && Round.Values.Any(p => p.FlashlightOn
                    && GhostVisionCheck(map, p.X, p.Y, p.Input.AimX, p.Input.AimY, ghost.X, ghost.Y));

                huntersView = ghost == null || ghostVisible
                    ? snapshots
                    : snapshots.Where(x => x.PlayerId != ghost.Session.PlayerId).ToList();

                if (ghostVisible)
                {
                    InLight += TickSeconds;
                    if (InLight >= MaxInLight)
                    {
                        over = GameOverReason.GhostCaught;
                    }
                }
                else
                {
                    InLight = Math.Max(InLight - TickSeconds, 0);
                }
                catchProgress = (byte)Math.Min(100f, InLight / MaxInLight * 100);

                if (ghost != null && over == null && ghost.PrimaryHeld && !ghostVisible)
                {
                    var item = NearestUncursedItem(map, ghost);
                    if (item >= 0)
                    {
                        curseSeconds[item] += TickSeconds;
                        if (curseSeconds[item] >= CurseSeconds)
                        {
                            curseMask |= (byte)(1 << item);
                            if (BitOperations.PopCount(curseMask) >= CursesToWin)
                            {
                                over = GameOverReason.Cursed;
                            }
                        }
                    }
                }
                curseProgress = curseSeconds.Select(s => (byte)Math.Min(100f, s / CurseSeconds * 100)).ToArray();

                if (msLeft == 0)
                {
                    over ??= GameOverReason.TimeUp;
                }
                if (over != null)
                {
                    gameOverPlayers = Round.Values.Select(p => new PlayerRole(p.Session.PlayerId, p.Role)).ToList();
                }

                recipients = Round.Values.ToList();
            }

            var ghostSnapshot = Packets.WriteSnapshot(new Snapshot(tick, timeLeft, snapshots, curseMask, catchProgress, curseProgress));
            var huntersSnapshot = Packets.WriteSnapshot(new Snapshot(tick, timeLeft, huntersView, curseMask, catchProgress, curseProgress));
            foreach (var player in recipients)
            {
                await transport.SendUnreliableAsync(PacketType.Snapshot,
                    player.Role == Role.Ghost ? ghostSnapshot : huntersSnapshot, player.Session.EndPoint);
            }

            if (over != null && gameOverPlayers != null)
            {
                InGame = false;
                var winner = over == GameOverReason.GhostCaught ? Role.Hunter : Role.Ghost;
                var payload = Packets.WriteGameOver(winner, over.Value, gameOverPlayers);
                foreach (var player in recipients)
                {
                    player.Session.IsReady = false;
                    await player.Session.Channel.SendAsync(PacketType.GameOver, payload);
                }
                await BroadcastStateAsync();
                Console.WriteLine($"Комната «{Name}»: раунд окончен — {over}, победил {(winner == Role.Ghost ? "призрак" : "охотники")}");
            }
        }
    }

    private int NearestUncursedItem(GameMap map, PlayerState ghost)
    {
        var best = -1;
        var bestDistance = CurseDistance;
        for (var i = 0; i < map.Items.Count; i++)
        {
            if ((curseMask & (1 << i)) != 0)
            {
                continue;
            }
            var distance = Vector2.Distance(new Vector2(ghost.X, ghost.Y), map.Items[i]);
            if (distance <= bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }
        return best;
    }
    public bool GhostVisionCheck(GameMap map, float posX, float posY, float aimX , float aimY , float ghostX , float ghostY)
    {
        var playerVector = new Vector2(posX, posY);
        var aimVector = new Vector2(aimX, aimY);
        var ghost = new Vector2(ghostX, ghostY);

        return (Flashlight.Covers(map, playerVector, aimVector, ghost));
    }
}
