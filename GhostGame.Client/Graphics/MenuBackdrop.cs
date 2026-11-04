using System.Numerics;
using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Живой фон всех меню: по особняку медленно плывёт камера, горят факелы, кто-то из-за края экрана
// шарит лучом фонаря, а сквозь стены проплывает призрак. Рисуется тем же кодом, что и сама игра.
// Это только картинка — ни с сервером, ни с правилами не связано.
public static class MenuBackdrop
{
    private static readonly LightRenderer Light = new();
    private static readonly Snapshot Nothing = new(0, 0, new List<SnapshotPlayer>(), 0);
    private static readonly bool[] NoCursing = new bool[Snapshot.ItemCount];
    private static GameMap? map;
    private static List<Vector2> lamps = new();

    public static bool IsReady => map != null;

    // Один раз после Assets.Load: какой особняк показывать
    public static void Init(GameMap mansion)
    {
        map = mansion;
        lamps = MapRenderer.FindLamps(mansion);
    }

    public static void Draw()
    {
        if (map == null)
        {
            return;
        }
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var t = (float)Raylib.GetTime();

        // клетка — целое число пикселей спрайта, примерно 36 клеток на ширину экрана
        var cell = Math.Max(2, width / 36 / Assets.SpriteSize) * Assets.SpriteSize;
        var visible = new Vector2(width, height) / cell;
        // камера медленно гуляет по особняку (полный круг — пара минут), за край карты не заезжает
        var focus = new Vector2(32 + 14 * MathF.Sin(t * 0.03f), 20 + 8 * MathF.Sin(t * 0.047f));
        focus = Vector2.Clamp(focus, visible / 2, new Vector2(GameMap.Width, GameMap.Height) - visible / 2);
        var origin = new Vector2(width, height) / 2 - focus * cell;
        var layout = new GameLayout(cell, new Vector2(MathF.Round(origin.X), MathF.Round(origin.Y)),
            new Rectangle(0, 0, width, height), new Rectangle(0, 0, 0, 0));

        // охотник стоит за левым нижним краем экрана и водит лучом туда-сюда
        var hunterAt = layout.ToCells(new Vector2(width * 0.1f, height * 1.03f));
        var sweep = (-55 + 28 * MathF.Sin(t * 0.6f)) * MathF.PI / 180;
        var hunter = new PlayerView(-1, "", hunterAt, hunterAt + new Vector2(MathF.Cos(sweep), MathF.Sin(sweep)) * 4,
            FlashlightOn: true, IsGhost: false, IsMe: false, Moving: false, Charge: 100, Overheated: false);

        // призрак плывёт по экрану восьмёркой — сквозь стены, ему можно
        var ghostAt = layout.ToCells(new Vector2(width * (0.5f + 0.34f * MathF.Sin(t * 0.19f)),
            height * (0.5f + 0.22f * MathF.Sin(t * 0.38f))));
        var heading = MathF.Cos(t * 0.19f); // куда сейчас летит по горизонтали — туда и смотрит
        var ghost = new PlayerView(-2, "", ghostAt, ghostAt + new Vector2(heading, 0), FlashlightOn: false,
            IsGhost: true, IsMe: false, Moving: false, Charge: 100, Overheated: false);

        MapRenderer.Draw(map, layout, Nothing, NoCursing, t);
        Light.Draw(layout, new[] { hunter }, map.Items, lamps, ghostView: false);
        PlayerRenderer.DrawBeams(new[] { hunter }, layout, ghostView: false);
        var glow = layout.ToScreen(ghostAt);
        Raylib.DrawCircleGradient((int)glow.X, (int)glow.Y, cell * 1.6f, Theme.GhostGlow, Raylib.Fade(Theme.GhostGlow, 0));
        PlayerRenderer.DrawBodies(new[] { ghost }, layout, t);

        // края экрана темнее — взгляд сам уходит в центр, к меню
        var edge = new Color(10, 6, 12, 230);
        var clear = Raylib.Fade(edge, 0);
        Raylib.DrawRectangleGradientV(0, 0, width, height / 4, edge, clear);
        Raylib.DrawRectangleGradientV(0, height - height / 4, width, height / 4, clear, edge);
        Raylib.DrawRectangleGradientH(0, 0, width / 5, height, edge, clear);
        Raylib.DrawRectangleGradientH(width - width / 5, 0, width / 5, height, clear, edge);
    }
}
