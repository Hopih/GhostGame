using System.Numerics;

namespace GhostGame.Shared;

// Карта особняка 32×20 клеток из текстового файла, одинакового у сервера и клиента (ТЗ 4.3):
// # стена, . пол, D дверь, 1–5 предметы, G точка появления призрака, H точки появления охотников.
// Координаты в клетках: клетка (x, y) занимает квадрат от (x, y) до (x + 1, y + 1), её центр — (x + 0.5, y + 0.5).
public sealed class GameMap
{
    public const int Width = 32;
    public const int Height = 20;
    public const string DefaultFileName = "map.txt";
    private const string AllowedCells = "#.D12345GH";

    private readonly char[,] cells; // cells[x, y]

    private GameMap(char[,] cells, Vector2 ghostSpawn, List<Vector2> hunterSpawns, Vector2[] items)
    {
        this.cells = cells;
        GhostSpawn = ghostSpawn;
        HunterSpawns = hunterSpawns;
        Items = items;
    }

    public Vector2 GhostSpawn { get; }

    public IReadOnlyList<Vector2> HunterSpawns { get; }

    // Items[0] — предмет «1», …, Items[4] — предмет «5»; номер в списке совпадает с битом в curseMask
    public IReadOnlyList<Vector2> Items { get; }

    public char this[int x, int y] => cells[x, y];

    // Карта лежит рядом с программой: map.txt копируется в папку сборки сервера и клиента
    public static GameMap Load(string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, DefaultFileName);
        return Parse(File.ReadAllLines(path));
    }

    public static GameMap Parse(string[] fileLines)
    {
        var lines = fileLines.Select(l => l.TrimEnd('\r')).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1); // пустые строки в конце файла не считаем
        }
        if (lines.Count != Height)
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

        return new GameMap(cells, ghostSpawn.Value, hunterSpawns, items.Select(i => i!.Value).ToArray());
    }

    // Стена ли в клетке; всё за краем карты тоже считается стеной
    public bool IsWall(int x, int y)
    {
        return x < 0 || y < 0 || x >= Width || y >= Height || cells[x, y] == '#';
    }

    // Можно ли поставить игрока радиуса radius с центром в (x, y), не задев ни одной стены.
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
                if (IsWall(cx, cy))
                {
                    return false;
                }
            }
        }
        return true;
    }
}
