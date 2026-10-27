using System.Numerics;
using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Где на экране карта и панель справа от неё. Считается каждый кадр от размера окна — окно можно растягивать.
// Клетка — целое число пикселей спрайта (×2, ×3…): при дробном увеличении пиксели вышли бы разной ширины.
public readonly record struct GameLayout(int Cell, Vector2 MapOrigin, Rectangle Panel)
{
    private const int Margin = 16;
    private const int MinPanelWidth = 200;
    private const int MaxPanelWidth = 360;

    public static GameLayout ForWindow()
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        // во сколько раз можно увеличить карту из спрайтов 16×16, чтобы справа ещё поместилась панель
        var scale = Math.Min(
            (width - MinPanelWidth - 3 * Margin) / (GameMap.Width * Assets.SpriteSize),
            (height - 2 * Margin) / (GameMap.Height * Assets.SpriteSize));
        var cell = Math.Max(1, scale) * Assets.SpriteSize;

        var mapWidth = GameMap.Width * cell;
        var mapHeight = GameMap.Height * cell;
        var panelWidth = Math.Clamp(width - mapWidth - 3 * Margin, MinPanelWidth, MaxPanelWidth);
        var left = Math.Max(Margin, (width - mapWidth - Margin - panelWidth) / 2); // карта и панель вместе по центру
        var top = Math.Max(Margin, (height - mapHeight) / 2);
        return new GameLayout(cell, new Vector2(left, top), new Rectangle(left + mapWidth + Margin, top, panelWidth, mapHeight));
    }

    public int MapWidth => GameMap.Width * Cell;

    public int MapHeight => GameMap.Height * Cell;

    public Rectangle Map => new(MapOrigin.X, MapOrigin.Y, MapWidth, MapHeight);

    // Клетки карты → пиксели окна (точка (0, 0) — левый верхний угол карты)
    public Vector2 ToScreen(Vector2 cells) => MapOrigin + cells * Cell;

    // Пиксели окна → клетки карты: так мышь превращается в «куда смотрит фонарь»
    public Vector2 ToCells(Vector2 screen) => (screen - MapOrigin) / Cell;

    public Rectangle CellRect(int x, int y) => new(MapOrigin.X + x * Cell, MapOrigin.Y + y * Cell, Cell, Cell);
}
