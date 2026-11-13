using System.Numerics;
using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

public readonly record struct GameLayout(int Cell, Vector2 MapOrigin, Rectangle Viewport, Rectangle Panel)
{
    private const int Margin = 16;
    private const int MinPanelWidth = 200;
    private const int MaxPanelWidth = 320;
    private const int MinVisibleWidth = 28;
    private const int MinVisibleHeight = 17;

    public static GameLayout ForWindow(Vector2 focus)
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var panelWidth = Math.Clamp(width / 6, MinPanelWidth, MaxPanelWidth);
        var viewport = new Rectangle(Margin, Margin, width - panelWidth - 3 * Margin, height - 2 * Margin);

        var scale = Math.Max(1, Math.Min(
            (int)viewport.Width / (MinVisibleWidth * Assets.SpriteSize),
            (int)viewport.Height / (MinVisibleHeight * Assets.SpriteSize)));
        var cell = scale * Assets.SpriteSize;

        var corner = new Vector2(
            CameraAxis(focus.X, viewport.Width / cell, GameMap.Width),
            CameraAxis(focus.Y, viewport.Height / cell, GameMap.Height));
        var origin = new Vector2(viewport.X, viewport.Y) - corner * cell;
        origin = new Vector2(MathF.Round(origin.X), MathF.Round(origin.Y));

        var panel = new Rectangle(viewport.X + viewport.Width + Margin, Margin, panelWidth, height - 2 * Margin);
        return new GameLayout(cell, origin, viewport, panel);
    }

    private static float CameraAxis(float focus, float visible, int mapSize)
    {
        if (visible >= mapSize)
        {
            return -(visible - mapSize) / 2;
        }
        return Math.Clamp(focus - visible / 2, 0, mapSize - visible);
    }

    public Rectangle Map => new(MapOrigin.X, MapOrigin.Y, GameMap.Width * Cell, GameMap.Height * Cell);

    public Vector2 ToScreen(Vector2 cells) => MapOrigin + cells * Cell;

    public Vector2 ToCells(Vector2 screen) => (screen - MapOrigin) / Cell;

    public Rectangle CellRect(int x, int y) => new(MapOrigin.X + x * Cell, MapOrigin.Y + y * Cell, Cell, Cell);

    public (int Left, int Top, int Right, int Bottom) VisibleCells()
    {
        var a = ToCells(new Vector2(Viewport.X, Viewport.Y));
        var b = ToCells(new Vector2(Viewport.X + Viewport.Width, Viewport.Y + Viewport.Height));
        return (Math.Max(0, (int)MathF.Floor(a.X)), Math.Max(0, (int)MathF.Floor(a.Y)),
            Math.Min(GameMap.Width - 1, (int)MathF.Floor(b.X)), Math.Min(GameMap.Height - 1, (int)MathF.Floor(b.Y)));
    }
}
