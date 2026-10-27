using System.Numerics;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Картинки и шрифт. Загружаются один раз после InitWindow: Raylib создаёт текстуры только при открытом окне.
// Спрайты — один атлас Kenney Tiny Dungeon (лицензия CC0, assets/tiny-dungeon-license.txt):
// 12 × 11 спрайтов по 16 × 16 пикселей, между ними 1 прозрачный пиксель.
public static class Assets
{
    public const int SpriteSize = 16;
    private const int Columns = 12;  // спрайтов в одном ряду атласа
    private const int Gap = 1;       // зазор между спрайтами: при увеличении соседний спрайт не «протекает» на край

    public static Texture2D Atlas { get; private set; }

    public static Font Font { get; private set; }

    public static void Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "tiny-dungeon.png");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Рядом с программой нет атласа спрайтов", path);
        }
        // фильтр по умолчанию — Point: при увеличении пиксели остаются чёткими квадратами, без размытия
        Atlas = Raylib.LoadTexture(path);
        Font = LoadFont();
    }

    // Нарисовать спрайт номер index в прямоугольник dest. flip — отразить по горизонтали (смотрит в другую сторону).
    public static void DrawSprite(int index, Rectangle dest, Color tint, bool flip = false)
    {
        var x = index % Columns * (SpriteSize + Gap);
        var y = index / Columns * (SpriteSize + Gap);
        var source = new Rectangle(x, y, flip ? -SpriteSize : SpriteSize, SpriteSize); // отрицательная ширина — отражение
        Raylib.DrawTexturePro(Atlas, source, dest, Vector2.Zero, 0, tint);
    }

    public static void DrawSprite(int index, Rectangle dest, bool flip = false)
    {
        DrawSprite(index, dest, Color.White, flip);
    }

    // Стандартный шрифт Raylib знает только латиницу — русский текст был бы «?».
    // Поэтому грузим системный TTF и просим в нём латиницу, кириллицу и несколько знаков.
    // Размер побольше (48), чтобы крупные заголовки не были мыльными.
    private static Font LoadFont()
    {
        var codepoints = Enumerable.Range(32, 95)                 // пробел, латиница, цифры, знаки
            .Concat(Enumerable.Range(0x400, 0x60))               // кириллица: А–я, Ё, ё
            .Concat("«»—–…№·→".Select(c => (int)c))
            .ToArray();

        foreach (var path in new[] { @"C:\Windows\Fonts\segoeui.ttf", @"C:\Windows\Fonts\arial.ttf", @"C:\Windows\Fonts\tahoma.ttf" })
        {
            if (File.Exists(path))
            {
                var font = Raylib.LoadFontEx(path, 48, codepoints, codepoints.Length);
                Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);
                return font;
            }
        }
        return Raylib.GetFontDefault(); // системного шрифта нет — будет только латиница
    }
}

// Номера спрайтов в атласе: слева направо, сверху вниз, начиная с нуля (12 в ряд)
public static class Sprites
{
    public const int Floor = 48;
    public const int FloorDots = 49;
    public const int FloorStones = 42;
    public const int WallTop = 0;     // тёмный верх стены
    public const int WallFace = 40;   // кирпичная грань стены
    public const int Chest = 89;      // предмет
    public const int Mimic = 92;      // проклятый предмет — сундук с языком
    public const int Ghost = 121;

    private static readonly int[] Hunters = { 96, 98, 99, 100, 112, 97, 85, 88 };

    // Спрайт охотника выбирается по playerId, поэтому у всех клиентов и в комнате, и в игре игрок выглядит одинаково
    public static int Hunter(int playerId) => Hunters[playerId % Hunters.Length];
}
