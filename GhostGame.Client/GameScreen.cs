using System.Numerics;
using GhostGame.Client.Graphics;
using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client;

// Экран игры (ТЗ 5.1): карта, предметы, игроки, лучи фонарей, таймер и счёт проклятых предметов.
// Сам ничего не решает: рисует последний Snapshot от сервера и читает нажатия, которые потом уходят в Input.
//
// Как пользоваться (создать на GameStart, дальше каждый кадр):
//   game.ApplySnapshot(snapshot)  — когда пришёл Snapshot
//   var input = game.ReadInput()  — что нажато сейчас; отправлять Input 20 раз в секунду
//   game.Draw()                   — между BeginDrawing и EndDrawing; true — после конца раунда нажали «В комнату»
public sealed class GameScreen
{
    public const int Cell = 40;                 // 1280 / 32 = 800 / 20 = 40 пикселей на клетку
    private const float PlayerRadius = 0.35f;   // в клетках
    private const float FlashlightLength = 5f;  // ТЗ 6.3: конус длиной 5 клеток
    private const float FlashlightAngle = 60f;  // и шириной 60°

    private static readonly Color WallColor = new(70, 70, 92, 255);
    private static readonly Color FloorColor = new(34, 34, 44, 255);
    private static readonly Color DoorColor = new(120, 88, 56, 255);
    private static readonly Color ItemColor = new(220, 190, 90, 255);
    private static readonly Color CursedColor = new(170, 60, 200, 255);
    private static readonly Color HunterColor = new(90, 160, 255, 255);
    private static readonly Color GhostColor = new(235, 235, 255, 210);
    private static readonly Color BeamColor = new(255, 240, 150, 70);       // луч глазами охотника
    private static readonly Color DangerBeamColor = new(255, 90, 90, 80);   // луч глазами призрака — опасная зона
    private static readonly Color MeColor = new(255, 220, 80, 255);
    private static readonly Color HudColor = new(0, 0, 0, 170);

    private readonly GameMap map;
    private readonly int myId;
    private readonly Role myRole;
    private readonly int ghostId;
    private readonly IReadOnlyDictionary<int, string> nicks;
    private Snapshot snapshot;

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

        // До первого Snapshot рисуем всех в точках появления. Охотнику призрака не показываем — туман войны.
        var players = spawns
            .Where(s => myRole == Role.Ghost || s.PlayerId != ghostId)
            .Select(s => new SnapshotPlayer(s.PlayerId, s.X, s.Y, s.X, s.Y, 0))
            .ToList();
        snapshot = new Snapshot(0, roundSeconds, players, 0);
    }

    public Role MyRole => myRole;

    // ТЗ 5.3: Snapshot применяется, только если его tick больше последнего применённого.
    // Опоздавший или повторный Snapshot просто отбрасывается. Возвращает true, если снимок применён.
    public bool ApplySnapshot(Snapshot next)
    {
        if (next.Tick <= snapshot.Tick)
        {
            return false;
        }
        snapshot = next;
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

        var mouse = Raylib.GetMousePosition();
        var actions = Raylib.IsMouseButtonDown(MouseButton.Left) ? InputState.ActionPrimary : (byte)0;
        return new InputState(moveX, moveY, mouse.X / Cell, mouse.Y / Cell, actions);
    }

    // Возвращает true, когда время раунда вышло и нажали «В комнату»
    public bool Draw()
    {
        DrawMap();
        DrawItems();
        DrawBeams();
        DrawPlayers();
        DrawHud();
        return snapshot.TimeLeft == 0 && DrawRoundOver();
    }

    private static bool DrawRoundOver()
    {
        Raylib.DrawRectangle(440, 320, 400, 150, new Color(0, 0, 0, 210));
        Ui.Text("Время вышло", 560, 340, 32);
        return Ui.Button(new Rectangle(540, 400, 200, 46), "В комнату");
    }

    private void DrawMap()
    {
        for (var y = 0; y < GameMap.Height; y++)
        {
            for (var x = 0; x < GameMap.Width; x++)
            {
                var color = map[x, y] switch
                {
                    '#' => WallColor,
                    'D' => DoorColor,
                    _ => FloorColor, // пол, точки появления и клетки предметов
                };
                Raylib.DrawRectangle(x * Cell, y * Cell, Cell, Cell, color);
            }
        }
    }

    private void DrawItems()
    {
        for (var i = 0; i < map.Items.Count; i++)
        {
            var center = ToPixels(map.Items[i]);
            var cursed = snapshot.IsCursed(i);
            Raylib.DrawCircleV(center, Cell * 0.3f, cursed ? CursedColor : ItemColor);
            var label = (i + 1).ToString();
            Ui.Text(label, center.X - Ui.MeasureText(label, 18) / 2, center.Y - 9, 18, Theme.Background);
        }
    }

    // Лучи фонарей: сектор круга радиусом 5 клеток и шириной 60° в сторону прицела.
    // Это только картинка — попадание в луч считает сервер (этап 6).
    private void DrawBeams()
    {
        var color = myRole == Role.Ghost ? DangerBeamColor : BeamColor;
        foreach (var p in snapshot.Players.Where(p => p.FlashlightOn))
        {
            var angle = MathF.Atan2(p.AimY - p.Y, p.AimX - p.X) * 180f / MathF.PI; // 0° — вправо, по часовой (ось Y вниз)
            Raylib.DrawCircleSector(ToPixels(p.X, p.Y), FlashlightLength * Cell,
                angle - FlashlightAngle / 2, angle + FlashlightAngle / 2, 24, color);
        }
    }

    private void DrawPlayers()
    {
        foreach (var p in snapshot.Players)
        {
            var center = ToPixels(p.X, p.Y);
            var radius = PlayerRadius * Cell;
            Raylib.DrawCircleV(center, radius, p.PlayerId == ghostId ? GhostColor : HunterColor);
            if (p.PlayerId == myId)
            {
                Raylib.DrawRing(center, radius + 2, radius + 5, 0, 360, 32, MeColor); // себя видно по жёлтому кольцу
            }

            var nick = nicks.TryGetValue(p.PlayerId, out var n) ? n : $"#{p.PlayerId}";
            Ui.Text(nick, center.X - Ui.MeasureText(nick, 16) / 2, center.Y - radius - 22, 16);
        }
    }

    // Строка сверху (поверх стены) и подсказка снизу
    private void DrawHud()
    {
        var role = myRole == Role.Ghost ? "Вы — призрак" : "Вы — охотник";
        var time = $"{snapshot.TimeLeft / 60}:{snapshot.TimeLeft % 60:00}";
        var cursed = System.Numerics.BitOperations.PopCount(snapshot.CurseMask);
        var hud = $"{role}   ·   {time}   ·   проклято {cursed} из 5";
        Raylib.DrawRectangle(8, 4, (int)Ui.MeasureText(hud, 22) + 24, 32, HudColor);
        Ui.Text(hud, 20, 9, 22);

        var hint = myRole == Role.Ghost
            ? "WASD — движение, ЛКМ у предмета — проклинать"
            : "WASD — движение, мышь — направление фонаря, ЛКМ — фонарь";
        Raylib.DrawRectangle(8, GameMap.Height * Cell - 36, (int)Ui.MeasureText(hint, 18) + 24, 30, HudColor);
        Ui.Text(hint, 20, GameMap.Height * Cell - 31, 18, Theme.Muted);
    }

    private static Vector2 ToPixels(Vector2 cells) => cells * Cell;

    private static Vector2 ToPixels(float x, float y) => new(x * Cell, y * Cell);
}
