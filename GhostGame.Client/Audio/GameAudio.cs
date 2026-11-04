using System.Numerics;
using GhostGame.Client.Graphics;
using GhostGame.Shared;

namespace GhostGame.Client.Audio;

// Звуки раунда. Сервер про звук ничего не знает: клиент сам «слышит» то, что и так видит в Snapshot,
// сравнивая этот кадр с прошлым — кто-то шагнул, фонарь включился, сундук прокляли, призрак попал в луч.
// Чем дальше источник от своего игрока, тем тише; дальше HearingRange клеток — не слышно.
public sealed class GameAudio
{
    private const float HearingRange = 14;
    private const float StepInterval = 0.34f;  // секунд между шагами на ходу

    private readonly GameMap map;
    private readonly Role myRole;
    private readonly Dictionary<int, float> stepTimers = new(); // playerId → когда следующий шаг
    private readonly float[] scratchTimers = new float[Snapshot.ItemCount];
    private bool started;
    private bool myFlashlight;
    private (int X, int Y) myCell = (-1, -1);
    private byte lastCurseMask;
    private bool ghostWasVisible = true;  // чтобы на старте раунда не прозвучало «замечен»
    private float heartbeatTimer;
    private float tickTimer;

    public GameAudio(GameMap map, Role myRole)
    {
        this.map = map;
        this.myRole = myRole;
    }

    // Раз в кадр. catching — поимка идёт прямо сейчас (процент вырос с прошлого снимка), cursing[i] — сундук i проклинают.
    public void Update(IReadOnlyList<PlayerView> players, Snapshot snapshot, IReadOnlyList<bool> cursing, bool catching, float dt)
    {
        var me = players.FirstOrDefault(p => p.IsMe);
        if (!me.IsMe)
        {
            return;
        }
        if (!started)
        {
            Sfx.Play(Sfx.RoundStart, 0.7f, 0.8f); // где-то скрипнула дверь — раунд начался
            started = true;
        }

        Footsteps(players, me, dt);

        // охотник прошёл через дверной проём — дверь скрипнула (призрак летает бесшумно)
        var cell = ((int)MathF.Floor(me.Position.X), (int)MathF.Floor(me.Position.Y));
        if (cell != myCell && myRole == Role.Hunter && !map.IsWall(cell.Item1, cell.Item2) && map[cell.Item1, cell.Item2] == 'D')
        {
            Sfx.PlayAny(Sfx.Creaks, 0.35f);
        }
        myCell = cell;

        // свой фонарь: щёлк при включении и выключении
        if (me.FlashlightOn != myFlashlight)
        {
            Sfx.Play(me.FlashlightOn ? Sfx.FlashOn : Sfx.FlashOff, 0.5f, me.FlashlightOn ? 1.2f : 1f);
            myFlashlight = me.FlashlightOn;
        }

        // сундук проклинают — низкий скрежет оттуда, тем тише, чем дальше
        for (var i = 0; i < cursing.Count; i++)
        {
            scratchTimers[i] -= dt;
            if (cursing[i] && scratchTimers[i] <= 0)
            {
                Sfx.PlayAny(Sfx.Scratches, 0.9f * Loudness(map.Items[i], me.Position), 0.55f);
                scratchTimers[i] = 0.45f;
            }
        }

        // сундук стал проклятым — хлопок крышки, слышно всем
        if (BitOperations.PopCount(snapshot.CurseMask) > BitOperations.PopCount(lastCurseMask))
        {
            Sfx.Play(Sfx.ChestSlam, 1f, 0.55f);
        }
        lastCurseMask = snapshot.CurseMask;

        // охотникам: призрак попал в луч и появился на экране
        if (myRole == Role.Hunter)
        {
            var visible = players.Any(p => p.IsGhost);
            if (visible && !ghostWasVisible)
            {
                Sfx.Play(Sfx.Spotted, 0.6f);
            }
            ghostWasVisible = visible;
        }

        // поимка: у призрака стучит сердце, у охотников щёлкает счётчик — чем ближе поимка, тем чаще
        var progress = snapshot.CatchProgress / 100f;
        if (myRole == Role.Ghost && snapshot.CatchProgress > 0)
        {
            heartbeatTimer -= dt;
            if (heartbeatTimer <= 0)
            {
                Sfx.Play(Sfx.Heartbeat, 0.5f + 0.5f * progress, 0.6f);
                heartbeatTimer = 0.9f - 0.6f * progress;
            }
        }
        else if (myRole == Role.Hunter && catching)
        {
            tickTimer -= dt;
            if (tickTimer <= 0)
            {
                Sfx.Play(Sfx.Tick, 0.6f);
                tickTimer = 0.35f - 0.27f * progress;
            }
        }
    }

    // Шаги охотников: свои и тех, кто рядом. Призрак не топает.
    private void Footsteps(IReadOnlyList<PlayerView> players, PlayerView me, float dt)
    {
        foreach (var p in players.Where(p => !p.IsGhost))
        {
            if (!p.Moving)
            {
                stepTimers[p.PlayerId] = 0; // остановился — следующий шаг сразу, как пойдёт
                continue;
            }
            var timer = stepTimers.GetValueOrDefault(p.PlayerId) - dt;
            if (timer <= 0)
            {
                Sfx.PlayAny(Sfx.Footsteps, p.IsMe ? 0.35f : 0.35f * Loudness(p.Position, me.Position));
                timer = StepInterval;
            }
            stepTimers[p.PlayerId] = timer;
        }
    }

    // Громкость по расстоянию: рядом — 1, на краю слышимости — 0
    private static float Loudness(Vector2 source, Vector2 listener)
    {
        return Math.Clamp(1 - Vector2.Distance(source, listener) / HearingRange, 0, 1);
    }
}
