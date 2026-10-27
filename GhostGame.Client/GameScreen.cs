using System.Numerics;
using GhostGame.Client.Graphics;
using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client;

// Экран игры (ТЗ 5.1). Здесь только состояние раунда: последние Snapshot от сервера, кто есть кто, что нажато.
// Рисуют классы из Graphics/ — отсюда им передаются готовые данные.
// Сам ничего не решает: показывает то, что прислал сервер, и читает нажатия, которые потом уходят в Input.
//
// Как пользоваться (создать на GameStart, дальше каждый кадр):
//   game.ApplySnapshot(snapshot)  — когда пришёл Snapshot
//   var input = game.ReadInput()  — что нажато сейчас; отправлять Input 20 раз в секунду
//   game.Draw(pingMs)             — между BeginDrawing и EndDrawing; true — после конца раунда нажали «В комнату»
public sealed class GameScreen
{
    private const float SnapshotInterval = 50; // сервер шлёт Snapshot раз в 50 мс

    private readonly GameMap map;
    private readonly int myId;
    private readonly Role myRole;
    private readonly int ghostId;
    private readonly IReadOnlyDictionary<int, string> nicks;
    private readonly List<RosterEntry> roster;
    private readonly LightRenderer light = new();
    private Snapshot previous;  // предпоследний применённый Snapshot
    private Snapshot current;   // последний применённый Snapshot
    private long currentAt;     // когда применили последний

    // spawns и roundSeconds — из GameStart, nicks — playerId → ник из последнего RoomState
    public GameScreen(GameMap map, int myId, Role myRole, ushort roundSeconds, List<PlayerSpawn> spawns,
        IReadOnlyDictionary<int, string> nicks)
    {
        this.map = map;
        this.myId = myId;
        this.myRole = myRole;
        this.nicks = nicks;

        // Роли других игроков в GameStart не приходят. Но призрак всегда появляется в точке G (ТЗ 6.1),
        // поэтому призрак — тот, чья точка появления совпадает с G.
        ghostId = myRole == Role.Ghost
            ? myId
            : spawns.FirstOrDefault(s => Vector2.Distance(new Vector2(s.X, s.Y), map.GhostSpawn) < 0.01f)?.PlayerId ?? -1;
        roster = spawns.Select(s => new RosterEntry(s.PlayerId, NickOf(s.PlayerId), s.PlayerId == ghostId)).ToList();

        // До первого Snapshot рисуем всех в точках появления. Охотнику призрака не показываем — туман войны.
        var players = spawns
            .Where(s => myRole == Role.Ghost || s.PlayerId != ghostId)
            .Select(s => new SnapshotPlayer(s.PlayerId, s.X, s.Y, s.X, s.Y, 0))
            .ToList();
        current = new Snapshot(0, roundSeconds, players, 0);
        previous = current;
        currentAt = Environment.TickCount64;
    }

    public Role MyRole => myRole;

    // ТЗ 5.3: Snapshot применяется, только если его tick больше последнего применённого.
    // Опоздавший или повторный Snapshot просто отбрасывается. Возвращает true, если снимок применён.
    public bool ApplySnapshot(Snapshot next)
    {
        if (next.Tick <= current.Tick)
        {
            return false;
        }
        previous = current;
        current = next;
        currentAt = Environment.TickCount64;
        return true;
    }

    // Что нажато прямо сейчас: WASD — направление, мышь — точка на карте (в клетках), ЛКМ — бит 0 actions
    public InputState ReadInput()
    {
        sbyte moveX = 0;
        sbyte moveY = 0;
        if (Raylib.IsKeyDown(KeyboardKey.A)) moveX--;
        if (Raylib.IsKeyDown(KeyboardKey.D)) moveX++;
        if (Raylib.IsKeyDown(KeyboardKey.W)) moveY--;
        if (Raylib.IsKeyDown(KeyboardKey.S)) moveY++;

        var aim = GameLayout.ForWindow().ToCells(Raylib.GetMousePosition()); // где на карте мышь — туда смотрит фонарь
        var actions = Raylib.IsMouseButtonDown(MouseButton.Left) ? InputState.ActionPrimary : (byte)0;
        return new InputState(moveX, moveY, aim.X, aim.Y, actions);
    }

    // Нарисовать раунд. Порядок важен: что нарисовано позже, то сверху.
    // Возвращает true, когда время раунда вышло и нажали «В комнату».
    public bool Draw(long pingMs)
    {
        var layout = GameLayout.ForWindow();
        var players = Players();
        var time = Raylib.GetTime();
        var ghostView = myRole == Role.Ghost;

        MapRenderer.Draw(map, layout, current.CurseMask, time);
        PlayerRenderer.DrawBodies(players, layout, time);
        light.Draw(layout, players, map.Items, ghostView);   // темнота поверх карты и игроков
        PlayerRenderer.DrawBeams(players, layout, ghostView);
        PlayerRenderer.DrawNames(players, layout);           // ники поверх темноты
        HudRenderer.Draw(layout, myRole, myId, current.TimeLeft, current.CurseMask, roster, pingMs);
        return current.TimeLeft == 0 && HudRenderer.DrawRoundOver(layout);
    }

    // Игроки для этого кадра. Snapshot приходит 20 раз в секунду, а кадров 60 — если рисовать позицию
    // из последнего снимка как есть, игроки двигаются рывками. Поэтому за 50 мс после прихода снимка
    // игрок плавно «доезжает» от позиции в предыдущем снимке до позиции в новом (интерполяция).
    private List<PlayerView> Players()
    {
        var sinceSnapshot = Environment.TickCount64 - currentAt;
        var t = Math.Clamp(sinceSnapshot / SnapshotInterval, 0f, 1f); // 0 — снимок только пришёл, 1 — уже доехали
        var result = new List<PlayerView>();
        foreach (var p in current.Players)
        {
            var position = new Vector2(p.X, p.Y);
            var aim = new Vector2(p.AimX, p.AimY);
            var moving = false;

            var before = previous.Players.FirstOrDefault(q => q.PlayerId == p.PlayerId);
            if (before != null) // в прошлом снимке игрока не было (например, призрак вышел из темноты) — рисуем сразу на месте
            {
                var was = new Vector2(before.X, before.Y);
                moving = Vector2.Distance(was, position) > 0.001f && sinceSnapshot < 150; // снимки перестали приходить — стоит
                position = Vector2.Lerp(was, position, t);
                aim = Vector2.Lerp(new Vector2(before.AimX, before.AimY), aim, t);
            }

            result.Add(new PlayerView(p.PlayerId, NickOf(p.PlayerId), position, aim, p.FlashlightOn,
                p.PlayerId == ghostId, p.PlayerId == myId, moving));
        }
        return result;
    }

    private string NickOf(int playerId)
    {
        return nicks.TryGetValue(playerId, out var nick) ? nick : $"#{playerId}";
    }
}
