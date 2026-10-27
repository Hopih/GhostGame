using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Пол, стены, двери и предметы — всё по символам map.txt
public static class MapRenderer
{
    private enum Side
    {
        Left,
        Right,
        Top,
    }

    public static void Draw(GameMap map, GameLayout layout, byte curseMask, double time)
    {
        for (var y = 0; y < GameMap.Height; y++)
        {
            for (var x = 0; x < GameMap.Width; x++)
            {
                var dest = layout.CellRect(x, y);
                if (map[x, y] == '#')
                {
                    DrawWall(map, x, y, dest, layout.Cell);
                    continue;
                }
                Assets.DrawSprite(FloorSprite(x, y), dest); // пол, точки появления, клетки предметов
                if (map[x, y] == 'D')
                {
                    DrawDoor(map, x, y, dest, layout.Cell);
                }
            }
        }
        DrawItems(map, layout, curseMask, time);
    }

    // Немного разнообразия на полу: изредка крапинки и камешки.
    // Зависит только от (x, y), поэтому от кадра к кадру не мигает.
    private static int FloorSprite(int x, int y)
    {
        var n = (x * 7919 + y * 104729 + x * y * 31) % 41;
        return n == 0 ? Sprites.FloorStones : n < 6 ? Sprites.FloorDots : Sprites.Floor;
    }

    // Вид «три четверти», как в паке Kenney: если под стеной проход — видна кирпичная грань стены,
    // иначе — тёмный верх стены. С тех сторон, где рядом не стена, рисуем каменный бортик.
    private static void DrawWall(GameMap map, int x, int y, Rectangle dest, int cell)
    {
        var face = !map.IsWall(x, y + 1);
        Assets.DrawSprite(face ? Sprites.WallFace : Sprites.WallTop, dest);

        var px = cell / (float)Assets.SpriteSize; // один пиксель спрайта на экране
        if (!map.IsWall(x - 1, y))
        {
            Rim(dest, px, Side.Left);
        }
        if (!map.IsWall(x + 1, y))
        {
            Rim(dest, px, Side.Right);
        }
        if (!map.IsWall(x, y - 1))
        {
            Rim(dest, px, Side.Top);
        }
    }

    // Бортик: от края клетки внутрь тёмная полоска, светлая, тёмная — по 2 пикселя спрайта (как в паке)
    private static void Rim(Rectangle cell, float px, Side side)
    {
        Color[] colors = { Theme.Outline, Theme.Stone, Theme.Outline };
        for (var i = 0; i < colors.Length; i++)
        {
            var offset = i * 2 * px;
            var strip = side switch
            {
                Side.Left => new Rectangle(cell.X + offset, cell.Y, 2 * px, cell.Height),
                Side.Right => new Rectangle(cell.X + cell.Width - offset - 2 * px, cell.Y, 2 * px, cell.Height),
                _ => new Rectangle(cell.X, cell.Y + offset, cell.Width, 2 * px),
            };
            Raylib.DrawRectangleRec(strip, colors[i]);
        }
    }

    // Дверной проём: деревянный порог из досок поперёк прохода
    private static void DrawDoor(GameMap map, int x, int y, Rectangle dest, int cell)
    {
        var px = cell / (float)Assets.SpriteSize;
        var passageVertical = !map.IsWall(x, y - 1) && !map.IsWall(x, y + 1); // сверху и снизу проход — идём сверху вниз

        var plank = passageVertical
            ? new Rectangle(dest.X, dest.Y + 4 * px, dest.Width, 8 * px)
            : new Rectangle(dest.X + 4 * px, dest.Y, 8 * px, dest.Height);
        Raylib.DrawRectangleRec(plank, Theme.Outline);
        var inner = passageVertical
            ? new Rectangle(plank.X, plank.Y + px, plank.Width, plank.Height - 2 * px)
            : new Rectangle(plank.X + px, plank.Y, plank.Width - 2 * px, plank.Height);
        Raylib.DrawRectangleRec(inner, Theme.Wood);

        for (var i = 1; i < 4; i++) // щели между четырьмя досками
        {
            var seam = passageVertical
                ? new Rectangle(inner.X + i * 4 * px, inner.Y, px, inner.Height)
                : new Rectangle(inner.X, inner.Y + i * 4 * px, inner.Width, px);
            Raylib.DrawRectangleRec(seam, Theme.WoodDark);
        }
    }

    // Предметы — сундуки со свечением. Проклятый — сундук-мимик и фиолетовое свечение (бит i в curseMask).
    private static void DrawItems(GameMap map, GameLayout layout, byte curseMask, double time)
    {
        for (var i = 0; i < map.Items.Count; i++)
        {
            var center = layout.ToScreen(map.Items[i]);
            var cursed = (curseMask & (1 << i)) != 0;
            var glow = cursed ? Theme.CurseGlow : Theme.ItemGlow;
            var pulse = 1 + 0.12f * MathF.Sin((float)time * 4 + i); // свечение чуть «дышит»
            Raylib.DrawCircleGradient((int)center.X, (int)center.Y, layout.Cell * 0.9f * pulse, glow, Raylib.Fade(glow, 0));

            var size = layout.Cell;
            Assets.DrawSprite(cursed ? Sprites.Mimic : Sprites.Chest,
                new Rectangle(center.X - size / 2f, center.Y - size / 2f, size, size));
        }
    }
}
