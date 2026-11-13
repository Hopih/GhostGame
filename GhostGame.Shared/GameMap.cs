using System.Globalization;
using System.Numerics;

namespace GhostGame.Shared;

public sealed class GameMap
{
    public const int Width = 64;
    public const int Height = 40;
    public const string DefaultFileName = "map.txt";

    private const string Walls = "#WVYE";
    private const string TallFurniture = "BKS";
    private const string LowFurniture = "TPbXRONAF";
    private const string AllowedCells = Walls + TallFurniture + LowFurniture + ".,=~Dcs12345GH";

    private readonly char[,] cells;

    private GameMap(char[,] cells, Vector2 ghostSpawn, List<Vector2> hunterSpawns, Vector2[] items, List<MapLabel> labels)
    {
        this.cells = cells;
        GhostSpawn = ghostSpawn;
        HunterSpawns = hunterSpawns;
        Items = items;
        Labels = labels;
    }

    public Vector2 GhostSpawn { get; }

    public IReadOnlyList<Vector2> HunterSpawns { get; }

    public IReadOnlyList<Vector2> Items { get; }

    public IReadOnlyList<MapLabel> Labels { get; }

    public char this[int x, int y] => cells[x, y];

    public static GameMap Load(string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, DefaultFileName);
        return Parse(File.ReadAllLines(path));
    }

    public static GameMap Parse(string[] fileLines)
    {
        var lines = fileLines.Select(l => l.TrimEnd('\r')).ToList();
        if (lines.Count < Height)
        {
            throw new InvalidDataException($"Карта должна быть из {Height} строк, а их {lines.Count}");
        }

        var cells = new char[Width, Height];
        Vector2? ghostSpawn = null;
        var hunterSpawns = new List<Vector2>();
        var items = new Vector2?[5];

        for (var y = 0; y < Height; y++)
        {
            if (lines[y].Length != Width)
            {
                throw new InvalidDataException($"Строка {y + 1} карты: нужно {Width} символов, а их {lines[y].Length}");
            }
            for (var x = 0; x < Width; x++)
            {
                var c = lines[y][x];
                if (!AllowedCells.Contains(c))
                {
                    throw new InvalidDataException($"Строка {y + 1}, позиция {x + 1}: неизвестный символ «{c}»");
                }
                cells[x, y] = c;

                var center = new Vector2(x + 0.5f, y + 0.5f);
                if (c == 'G')
                {
                    if (ghostSpawn != null)
                    {
                        throw new InvalidDataException("На карте больше одной точки G");
                    }
                    ghostSpawn = center;
                }
                else if (c == 'H')
                {
                    hunterSpawns.Add(center);
                }
                else if (c >= '1' && c <= '5')
                {
                    var index = c - '1';
                    if (items[index] != null)
                    {
                        throw new InvalidDataException($"Предмет {c} встречается на карте дважды");
                    }
                    items[index] = center;
                }
            }
        }

        if (ghostSpawn == null)
        {
            throw new InvalidDataException("На карте нет точки появления призрака G");
        }
        if (hunterSpawns.Count < 4)
        {
            throw new InvalidDataException($"На карте нужно не меньше 4 точек H (до 4 охотников), а их {hunterSpawns.Count}");
        }
        if (items.Any(i => i == null))
        {
            throw new InvalidDataException("На карте должны быть все предметы от 1 до 5");
        }

        return new GameMap(cells, ghostSpawn.Value, hunterSpawns, items.Select(i => i!.Value).ToArray(),
            ParseLabels(lines.Skip(Height)));
    }

    private static List<MapLabel> ParseLabels(IEnumerable<string> lines)
    {
        var labels = new List<MapLabel>();
        foreach (var line in lines.Where(l => l.Length > 0))
        {
            var parts = line.Split(' ', 3);
            if (parts.Length < 3 || !line.StartsWith('@')
                || !float.TryParse(parts[0][1..], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                throw new InvalidDataException($"Строка подписи «{line}»: нужно «@x y Название»");
            }
            labels.Add(new MapLabel(new Vector2(x, y), parts[2]));
        }
        return labels;
    }

    public bool IsWall(int x, int y)
    {
        return x < 0 || y < 0 || x >= Width || y >= Height || Walls.Contains(cells[x, y]);
    }

    public bool IsSolid(int x, int y)
    {
        return IsWall(x, y) || TallFurniture.Contains(cells[x, y]) || LowFurniture.Contains(cells[x, y]);
    }

    public bool BlocksLight(int x, int y)
    {
        return IsWall(x, y) || TallFurniture.Contains(cells[x, y]);
    }

    // Можно ли поставить игрока радиуса radius с центром в (x, y), не задев ни стены, ни мебели.
    // Игрок считается квадратом со стороной 2·radius: проверяем все клетки, которые он накрывает.
    public bool CanStandAt(float x, float y, float radius)
    {
        var left = (int)MathF.Floor(x - radius);
        var right = (int)MathF.Floor(x + radius);
        var top = (int)MathF.Floor(y - radius);
        var bottom = (int)MathF.Floor(y + radius);
        for (var cy = top; cy <= bottom; cy++)
        {
            for (var cx = left; cx <= right; cx++)
            {
                if (IsSolid(cx, cy))
                {
                    return false;
                }
            }
        }
        return true;
    }
}

public record MapLabel(Vector2 Position, string Text);
