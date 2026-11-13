using System.Numerics;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

public static class Assets
{
    public const int SpriteSize = 16;
    private const int Columns = 12;
    private const int Gap = 1;

    public static Texture2D Atlas { get; private set; }

    public static Font Font { get; private set; }

    public static void Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "tiny-dungeon.png");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Рядом с программой нет атласа спрайтов", path);
        }
        Atlas = Raylib.LoadTexture(path);
        Font = LoadFont();
    }

    public static void DrawSprite(int index, Rectangle dest, Color tint, bool flip = false)
    {
        var x = index % Columns * (SpriteSize + Gap);
        var y = index / Columns * (SpriteSize + Gap);
        var source = new Rectangle(x, y, flip ? -SpriteSize : SpriteSize, SpriteSize);
        Raylib.DrawTexturePro(Atlas, source, dest, Vector2.Zero, 0, tint);
    }

    public static void DrawSprite(int index, Rectangle dest, bool flip = false)
    {
        DrawSprite(index, dest, Color.White, flip);
    }

    private static Font LoadFont()
    {
        var codepoints = Enumerable.Range(32, 95)
            .Concat(Enumerable.Range(0x400, 0x60))
            .Concat("«»—–…№·→".Select(c => (int)c))
            .ToArray();

        foreach (var path in new[] { @"C:\Windows\Fonts\segoeui.ttf", @"C:\Windows\Fonts\arial.ttf", @"C:\Windows\Fonts\tahoma.ttf" })
        {
            if (File.Exists(path))
            {
                var font = Raylib.LoadFontEx(path, 72, codepoints, codepoints.Length);
                Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);
                return font;
            }
        }
        return Raylib.GetFontDefault();
    }
}

public static class Sprites
{
    public const int Floor = 48;
    public const int FloorDots = 49;
    public const int FloorStones = 42;
    public const int WallTop = 0;
    public const int WallFace = 40;
    public const int Chest = 89;
    public const int Mimic = 92;
    public const int Ghost = 121;

    public const int Cabinet = 75;
    public const int Pillar = 31;
    public const int Table = 72;
    public const int Chair = 73;
    public const int Barrel = 82;
    public const int Cross = 64;
    public const int Grave = 65;
    public const int Cauldron = 56;
    public const int Anvil = 74;
    public const int Basin = 32;
    public const int BannerWall = 29;
    public const int DoorLeft = 46;
    public const int DoorRight = 47;
    public static readonly int[] Potions = { 113, 114, 115, 116 };
    public static readonly int[] Weapons = { 104, 117, 106, 118, 105 };
    public static readonly int[] Critters = { 122, 123, 120 };

    private static readonly int[] Hunters = { 96, 98, 99, 100, 112, 97, 85, 88 };

    public static int Hunter(int playerId) => Hunters[playerId % Hunters.Length];
}
