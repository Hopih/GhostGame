using Raylib_cs;

namespace GhostGame.Client.Graphics;

public static class Theme
{
    public static readonly Color Background = new(30, 21, 30, 255);
    public static readonly Color Veil = new(30, 21, 30, 220);
    public static readonly Color MenuVeil = new(20, 13, 20, 70);
    public static readonly Color GhostGlow = new(210, 225, 255, 70);
    public static readonly Color TitleShadow = new(0, 0, 0, 170);
    public static readonly Color Panel = new(46, 32, 42, 245);
    public static readonly Color PanelBorder = new(84, 56, 70, 255);
    public static readonly Color Row = new(60, 42, 54, 255);
    public static readonly Color RowHover = new(86, 58, 72, 255);
    public static readonly Color Field = new(28, 19, 27, 255);
    public static readonly Color Button = new(118, 59, 54, 255);
    public static readonly Color ButtonHover = new(150, 78, 66, 255);
    public static readonly Color ButtonDisabled = new(64, 48, 58, 255);
    public static readonly Color Accent = new(234, 165, 108, 255);
    public static readonly Color AccentHover = new(254, 201, 156, 255);
    public static readonly Color Text = new(246, 232, 216, 255);
    public static readonly Color Muted = new(166, 146, 154, 255);
    public static readonly Color Error = new(236, 98, 92, 255);
    public static readonly Color Good = new(130, 200, 90, 255);

    public static readonly Color Outline = new(63, 38, 49, 255);
    public static readonly Color Stone = new(192, 203, 220, 255);
    public static readonly Color Wood = new(189, 108, 74, 255);
    public static readonly Color WoodDark = new(118, 59, 54, 255);
    public static readonly Color Shadow = new(0, 0, 0, 80);
    public static readonly Color GhostTint = new(255, 255, 255, 180);
    public static readonly Color Me = new(255, 214, 90, 255);
    public static readonly Color Beam = new(255, 236, 160, 40);
    public static readonly Color DangerBeam = new(255, 70, 70, 70);
    public static readonly Color ItemGlow = new(255, 200, 110, 130);
    public static readonly Color CurseGlow = new(190, 80, 255, 160);
    public static readonly Color Plate = new(20, 13, 20, 190);
    public static readonly Color Overlay = new(14, 9, 14, 200);

    public static readonly Color[] StoneTiles = { new(104, 110, 128, 255), new(96, 102, 120, 255), new(110, 116, 134, 255) };
    public static readonly Color StoneLight = new(128, 134, 150, 255);
    public static readonly Color StoneSeam = new(70, 74, 90, 255);
    public static readonly Color Parquet = new(166, 110, 72, 255);
    public static readonly Color ParquetDark = new(154, 100, 64, 255);
    public static readonly Color ParquetSeam = new(122, 78, 52, 255);
    public static readonly Color Carpet = new(122, 36, 46, 255);
    public static readonly Color CarpetDark = new(96, 26, 36, 255);
    public static readonly Color CarpetBorder = new(206, 160, 80, 255);
    public static readonly Color ShelfWood = new(118, 70, 48, 255);
    public static readonly Color ShelfBack = new(60, 36, 30, 255);
    public static readonly Color[] Books =
    {
        new(172, 60, 58, 255), new(70, 110, 170, 255), new(84, 150, 92, 255),
        new(206, 170, 84, 255), new(140, 82, 160, 255), new(190, 120, 70, 255),
    };
    public static readonly Color Flame = new(255, 170, 60, 255);
    public static readonly Color FlameCore = new(255, 240, 170, 255);
    public static readonly Color MagicGlow = new(110, 255, 170, 120);
    public static readonly Color RoomLabel = new(246, 232, 216, 120);

    public static readonly Color MiniWall = new(28, 19, 27, 255);
    public static readonly Color MiniSand = new(150, 110, 78, 255);
    public static readonly Color MiniStone = new(92, 98, 116, 255);
    public static readonly Color MiniWood = new(118, 76, 54, 255);
    public static readonly Color MiniCarpet = new(116, 40, 50, 255);
    public static readonly Color MiniFurniture = new(58, 42, 50, 255);
    public static readonly Color MiniHunter = new(110, 170, 255, 255);
}
