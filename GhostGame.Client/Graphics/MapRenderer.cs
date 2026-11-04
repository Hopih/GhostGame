using System.Numerics;
using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Пол, стены, мебель, двери, факелы, подписи комнат и предметы — всё по символам map.txt.
// Рисуются только клетки, которые сейчас попадают в окно: карта больше экрана.
public static class MapRenderer
{
    private enum Side
    {
        Left,
        Right,
        Top,
    }

    // snapshot — откуда маска и прогресс проклятий; cursing[i] — сундук i проклинают прямо сейчас
    public static void Draw(GameMap map, GameLayout layout, Snapshot snapshot, IReadOnlyList<bool> cursing, double time)
    {
        var (left, top, right, bottom) = layout.VisibleCells();

        // проход 1: пол везде, где не стена (под мебелью и дверью — пол её комнаты)
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                if (!map.IsWall(x, y))
                {
                    DrawFloor(map, x, y, layout.CellRect(x, y), layout.Cell);
                }
            }
        }
        DrawLabels(map, layout);

        // проход 2: стены, двери, мебель и декор поверх пола
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                var dest = layout.CellRect(x, y);
                if (map.IsWall(x, y))
                {
                    DrawWall(map, x, y, dest, layout.Cell, time);
                }
                else if (map[x, y] == 'D')
                {
                    DrawDoor(map, x, y, dest, layout.Cell);
                }
                else
                {
                    DrawFurniture(map[x, y], x, y, dest, layout.Cell, time);
                }
            }
        }
        DrawItems(map, layout, snapshot, cursing, time);
    }

    // Где на карте источники света: факел светит в комнату под собой, чаша с огнём — вокруг себя
    public static List<Vector2> FindLamps(GameMap map)
    {
        var lamps = new List<Vector2>();
        for (var y = 0; y < GameMap.Height; y++)
        {
            for (var x = 0; x < GameMap.Width; x++)
            {
                if (map[x, y] == 'W' && !map.IsWall(x, y + 1))
                {
                    lamps.Add(new Vector2(x + 0.5f, y + 1.2f));
                }
                else if (map[x, y] == 'F')
                {
                    lamps.Add(new Vector2(x + 0.5f, y + 0.5f));
                }
            }
        }
        return lamps;
    }

    // ---------- пол ----------

    private static bool IsFloor(char c) => c is '.' or ',' or '=' or '~';

    // Какой пол в клетке. Под мебелью, дверью или предметом — тот пол, которого больше всего среди 8 соседей;
    // при равенстве — не ковёр, чтобы ковёр не «вытекал» под стулья, стоящие у его края.
    private static char FloorOf(GameMap map, int x, int y)
    {
        if (IsFloor(map[x, y]))
        {
            return map[x, y];
        }
        var best = '.';
        var bestCount = 0;
        foreach (var floor in ".,=~")
        {
            var count = 0;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (!map.IsWall(x + dx, y + dy) && map[x + dx, y + dy] == floor)
                    {
                        count++;
                    }
                }
            }
            if (count > bestCount)
            {
                (best, bestCount) = (floor, count); // ковёр проверяется последним и побеждает только при перевесе
            }
        }
        return best;
    }

    private static void DrawFloor(GameMap map, int x, int y, Rectangle dest, int cell)
    {
        var px = cell / (float)Assets.SpriteSize; // один пиксель спрайта на экране
        switch (FloorOf(map, x, y))
        {
            case ',':
                StoneFloor(x, y, dest, px);
                break;
            case '=':
                Parquet(x, y, dest, px);
                break;
            case '~':
                Carpet(map, x, y, dest, px);
                break;
            default:
                Assets.DrawSprite(SandSprite(x, y), dest);
                break;
        }
    }

    // Песок: изредка крапинки и камешки. Зависит только от (x, y), поэтому от кадра к кадру не мигает.
    private static int SandSprite(int x, int y)
    {
        var n = Hash(x, y) % 41;
        return n == 0 ? Sprites.FloorStones : n < 6 ? Sprites.FloorDots : Sprites.Floor;
    }

    // Каменные плиты: 4 плитки на клетку, у каждой свой оттенок, светлая кромка сверху и тёмные швы
    private static void StoneFloor(int x, int y, Rectangle dest, float px)
    {
        for (var i = 0; i < 2; i++)
        {
            for (var j = 0; j < 2; j++)
            {
                var tile = new Rectangle(dest.X + i * 8 * px, dest.Y + j * 8 * px, 8 * px, 8 * px);
                Raylib.DrawRectangleRec(tile, Theme.StoneTiles[Hash(x * 2 + i, y * 2 + j) % Theme.StoneTiles.Length]);
                Raylib.DrawRectangleRec(tile with { Height = px }, Theme.StoneLight);
                Raylib.DrawRectangleRec(tile with { Y = tile.Y + tile.Height - px, Height = px }, Theme.StoneSeam);
                Raylib.DrawRectangleRec(tile with { X = tile.X + tile.Width - px, Width = px }, Theme.StoneSeam);
            }
        }
    }

    // Паркет: длинные доски по 4 пикселя, у каждой свой оттенок; стык доски — примерно раз в три клетки
    private static void Parquet(int x, int y, Rectangle dest, float px)
    {
        for (var row = 0; row < 4; row++)
        {
            var plank = new Rectangle(dest.X, dest.Y + row * 4 * px, dest.Width, 4 * px);
            var board = (x + row * 5 + y * 7) / 3; // номер доски в ряду: соседние клетки часто на одной доске
            Raylib.DrawRectangleRec(plank, (Hash(board, y * 4 + row) & 1) == 0 ? Theme.Parquet : Theme.ParquetDark);
            Raylib.DrawRectangleRec(plank with { Y = plank.Y + plank.Height - px, Height = px }, Theme.ParquetSeam);
            if ((x + row * 5 + y * 7) % 3 == 0) // здесь доска кончается
            {
                Raylib.DrawRectangleRec(new Rectangle(dest.X, plank.Y, px, plank.Height), Theme.ParquetSeam);
            }
        }
    }

    // Ковёр: тёмно-красный с ромбиками, по краю — золотая кайма (там, где дальше уже не ковёр)
    private static void Carpet(GameMap map, int x, int y, Rectangle dest, float px)
    {
        Raylib.DrawRectangleRec(dest, Theme.Carpet);
        Raylib.DrawPoly(new Vector2(dest.X + dest.Width / 2, dest.Y + dest.Height / 2), 4, 3 * px, 0, Theme.CarpetDark);

        var border = 2 * px;
        if (!CarpetAt(map, x - 1, y)) Raylib.DrawRectangleRec(dest with { Width = border }, Theme.CarpetBorder);
        if (!CarpetAt(map, x + 1, y)) Raylib.DrawRectangleRec(dest with { X = dest.X + dest.Width - border, Width = border }, Theme.CarpetBorder);
        if (!CarpetAt(map, x, y - 1)) Raylib.DrawRectangleRec(dest with { Height = border }, Theme.CarpetBorder);
        if (!CarpetAt(map, x, y + 1)) Raylib.DrawRectangleRec(dest with { Y = dest.Y + dest.Height - border, Height = border }, Theme.CarpetBorder);
    }

    private static bool CarpetAt(GameMap map, int x, int y) => !map.IsWall(x, y) && FloorOf(map, x, y) == '~';

    // Названия комнат бледно на полу — чтобы в большом особняке было понятно, где ты
    private static void DrawLabels(GameMap map, GameLayout layout)
    {
        var size = MathF.Max(16, layout.Cell * 0.5f);
        foreach (var label in map.Labels)
        {
            var at = layout.ToScreen(label.Position);
            Ui.TextCentered(label.Text, at.X, at.Y - size / 2, size, Theme.RoomLabel);
        }
    }

    // ---------- стены ----------

    // Вид «три четверти», как в паке Kenney: если под стеной проход — видна кирпичная грань стены,
    // иначе — тёмный верх стены. С тех сторон, где рядом не стена, рисуем каменный бортик.
    // На грани стены бывают украшения: факел (W), знамя (V), оружие (Y); E — створки парадного входа.
    private static void DrawWall(GameMap map, int x, int y, Rectangle dest, int cell, double time)
    {
        var c = map[x, y];
        var face = !map.IsWall(x, y + 1);
        Assets.DrawSprite(face ? (c == 'V' ? Sprites.BannerWall : Sprites.WallFace) : Sprites.WallTop, dest);

        var px = cell / (float)Assets.SpriteSize;
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

        if (face && c == 'W')
        {
            DrawTorch(dest, px, time, x * 7 + y);
        }
        else if (face && c == 'Y')
        {
            Assets.DrawSprite(Sprites.Weapons[x % Sprites.Weapons.Length], Shrink(dest, 0.75f));
        }
        else if (c == 'E')
        {
            Assets.DrawSprite(x % 2 == 0 ? Sprites.DoorLeft : Sprites.DoorRight, dest);
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

    // Факел на стене: деревянная ручка и пламя, которое чуть дрожит
    private static void DrawTorch(Rectangle dest, float px, double time, int seed)
    {
        var centerX = dest.X + dest.Width / 2;
        Raylib.DrawRectangleRec(new Rectangle(centerX - px, dest.Y + 8 * px, 2 * px, 6 * px), Theme.WoodDark);
        var flicker = 1 + 0.15f * MathF.Sin((float)time * 12 + seed * 1.7f);
        var flame = new Vector2(centerX, dest.Y + 7 * px);
        Raylib.DrawCircleGradient((int)flame.X, (int)flame.Y, 7 * px * flicker, Raylib.Fade(Theme.Flame, 0.45f), Raylib.Fade(Theme.Flame, 0));
        Raylib.DrawCircleV(flame, 2.2f * px * flicker, Theme.Flame);
        Raylib.DrawCircleV(flame with { Y = flame.Y + 0.5f * px }, 1.2f * px, Theme.FlameCore);
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

    // ---------- мебель ----------

    private static void DrawFurniture(char c, int x, int y, Rectangle dest, int cell, double time)
    {
        var px = cell / (float)Assets.SpriteSize;
        switch (c)
        {
            case 'B':
                Bookshelf(x, y, dest, px);
                break;
            case 'K':
                Assets.DrawSprite(Sprites.Cabinet, dest);
                break;
            case 'S':
                Assets.DrawSprite(Sprites.Pillar, dest);
                break;
            case 'T':
                Assets.DrawSprite(Sprites.Table, dest);
                break;
            case 'P': // стол с зельем
                Assets.DrawSprite(Sprites.Table, dest);
                Assets.DrawSprite(Sprites.Potions[(x + y) % Sprites.Potions.Length], Shrink(dest, 0.6f) with { Y = dest.Y });
                break;
            case 'b':
                Assets.DrawSprite(Sprites.Barrel, dest);
                break;
            case 'X':
                Assets.DrawSprite(Sprites.Cross, dest);
                break;
            case 'R':
                Assets.DrawSprite(Sprites.Grave, dest);
                break;
            case 'O':
                Assets.DrawSprite(Sprites.Cauldron, dest);
                break;
            case 'N':
                Assets.DrawSprite(Sprites.Anvil, dest);
                break;
            case 'A': // алтарь со свечами
                Assets.DrawSprite(Sprites.Pillar, dest);
                Candle(new Vector2(dest.X + 5 * px, dest.Y + 3 * px), px, time, x);
                Candle(new Vector2(dest.X + 11 * px, dest.Y + 3 * px), px, time, x + 3);
                break;
            case 'F': // чаша с зелёным огнём — светится и «дышит»
                var pulse = 1 + 0.1f * MathF.Sin((float)time * 3 + x);
                var center = new Vector2(dest.X + dest.Width / 2, dest.Y + dest.Height / 2);
                Raylib.DrawCircleGradient((int)center.X, (int)center.Y, cell * 1.1f * pulse, Theme.MagicGlow, Raylib.Fade(Theme.MagicGlow, 0));
                Assets.DrawSprite(Sprites.Basin, dest);
                break;
            case 'c':
                Assets.DrawSprite(Sprites.Chair, Shrink(dest, 0.8f));
                break;
            case 's': // паук, крыса или мышь — чуть шевелится
                var wiggle = MathF.Sin((float)time * 3 + x * 2) * px;
                Assets.DrawSprite(Sprites.Critters[Hash(x, y) % Sprites.Critters.Length], Shrink(dest, 0.7f) with { X = dest.X + dest.Width * 0.15f + wiggle });
                break;
        }
    }

    // Книжный шкаф: тёмная рама, две полки, корешки книг разного цвета и высоты
    private static void Bookshelf(int x, int y, Rectangle dest, float px)
    {
        Raylib.DrawRectangleRec(dest, Theme.Outline);
        Raylib.DrawRectangleRec(new Rectangle(dest.X + px, dest.Y + px, dest.Width - 2 * px, dest.Height - 2 * px), Theme.ShelfWood);
        for (var shelf = 0; shelf < 2; shelf++)
        {
            var top = dest.Y + (2 + shelf * 7) * px;
            Raylib.DrawRectangleRec(new Rectangle(dest.X + 2 * px, top, dest.Width - 4 * px, 5 * px), Theme.ShelfBack);
            for (var i = 0; i < 6; i++)
            {
                var h = Hash(x * 7 + i, y * 3 + shelf);
                var height = 3 + h % 3; // книги высотой 3–5 пикселей
                Raylib.DrawRectangleRec(new Rectangle(dest.X + (2 + i * 2) * px, top + (5 - height) * px, 2 * px, height * px),
                    Theme.Books[h % Theme.Books.Length]);
            }
            Raylib.DrawRectangleRec(new Rectangle(dest.X + px, top + 5 * px, dest.Width - 2 * px, px), Theme.WoodDark); // доска полки
        }
    }

    private static void Candle(Vector2 at, float px, double time, int seed)
    {
        Raylib.DrawRectangleRec(new Rectangle(at.X - px * 0.5f, at.Y, px, 3 * px), Theme.Stone);
        var flicker = 1 + 0.2f * MathF.Sin((float)time * 14 + seed);
        Raylib.DrawCircleV(at with { Y = at.Y - px }, 1.2f * px * flicker, Theme.Flame);
    }

    // ---------- предметы ----------

    // Предметы — сундуки со свечением. Проклятый — сундук-мимик и фиолетовое свечение (бит i в curseMask).
    // Начатое проклятие — фиолетовая полоска над сундуком; тот, что проклинают прямо сейчас, трясётся и мигает.
    private static void DrawItems(GameMap map, GameLayout layout, Snapshot snapshot, IReadOnlyList<bool> cursing, double time)
    {
        var cell = layout.Cell;
        for (var i = 0; i < map.Items.Count; i++)
        {
            var center = layout.ToScreen(map.Items[i]);
            var cursed = snapshot.IsCursed(i);
            var now = cursing[i];
            var glow = cursed || now ? Theme.CurseGlow : Theme.ItemGlow;
            var pulse = now
                ? 1.3f + 0.3f * MathF.Sin((float)time * 14)          // проклинают — свечение быстро бьётся
                : 1 + 0.12f * MathF.Sin((float)time * 4 + i);         // иначе чуть «дышит»
            Raylib.DrawCircleGradient((int)center.X, (int)center.Y, cell * 0.9f * pulse, glow, Raylib.Fade(glow, 0));

            var shake = now ? MathF.Sin((float)time * 40) * cell * 0.04f : 0;
            Assets.DrawSprite(cursed ? Sprites.Mimic : Sprites.Chest,
                new Rectangle(center.X - cell / 2f + shake, center.Y - cell / 2f, cell, cell));

            var progress = snapshot.CurseProgressOf(i);
            if (!cursed && progress > 0)
            {
                var bar = new Rectangle(center.X - cell * 0.45f, center.Y - cell * 0.8f, cell * 0.9f, MathF.Max(5, cell * 0.14f));
                Ui.ProgressBar(bar, progress / 100f, Theme.CurseGlow with { A = 255 });
            }
        }
    }

    // ---------- мелочи ----------

    // Тот же прямоугольник, уменьшенный в scale раз, по центру
    private static Rectangle Shrink(Rectangle r, float scale)
    {
        var w = r.Width * scale;
        var h = r.Height * scale;
        return new Rectangle(r.X + (r.Width - w) / 2, r.Y + (r.Height - h) / 2, w, h);
    }

    // Псевдослучайное число по координатам: одно и то же для клетки в каждом кадре — ничего не мигает
    private static int Hash(int x, int y) => ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff;
}
