using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Все цвета клиента в одном месте: поменять оформление — значит поменять цвета здесь, а не искать по всем файлам
public static class Theme
{
    public static readonly Color Background = new(24, 24, 32, 255);
    public static readonly Color Panel = new(44, 44, 58, 255);
    public static readonly Color PanelHover = new(64, 64, 84, 255);
    public static readonly Color Accent = new(120, 160, 255, 255);
    public static readonly Color Text = new(230, 230, 240, 255);
    public static readonly Color Muted = new(150, 150, 165, 255);
    public static readonly Color Error = new(255, 110, 110, 255);
}
