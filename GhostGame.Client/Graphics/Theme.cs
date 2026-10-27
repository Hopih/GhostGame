using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Все цвета клиента в одном месте: поменять оформление — значит поменять цвета здесь, а не искать по всем файлам.
// Палитра взята из спрайтов Kenney Tiny Dungeon, поэтому меню и игра выглядят как одно целое.
public static class Theme
{
    // --- меню ---
    public static readonly Color Background = new(30, 21, 30, 255);
    public static readonly Color Veil = new(30, 21, 30, 220);            // тёмная вуаль поверх пола на фоне меню
    public static readonly Color Panel = new(46, 32, 42, 245);
    public static readonly Color PanelBorder = new(84, 56, 70, 255);
    public static readonly Color Row = new(60, 42, 54, 255);             // строка списка
    public static readonly Color RowHover = new(86, 58, 72, 255);
    public static readonly Color Field = new(28, 19, 27, 255);           // поле ввода, окно чата
    public static readonly Color Button = new(118, 59, 54, 255);         // как верх стены
    public static readonly Color ButtonHover = new(150, 78, 66, 255);
    public static readonly Color ButtonDisabled = new(64, 48, 58, 255);
    public static readonly Color Accent = new(234, 165, 108, 255);       // песок пола: главные кнопки, фокус
    public static readonly Color AccentHover = new(254, 201, 156, 255);
    public static readonly Color Text = new(246, 232, 216, 255);
    public static readonly Color Muted = new(166, 146, 154, 255);
    public static readonly Color Error = new(236, 98, 92, 255);
    public static readonly Color Good = new(130, 200, 90, 255);

    // --- игра ---
    public static readonly Color Outline = new(63, 38, 49, 255);         // тёмная обводка спрайтов
    public static readonly Color Stone = new(192, 203, 220, 255);        // светлый камень бортиков стен
    public static readonly Color Wood = new(189, 108, 74, 255);
    public static readonly Color WoodDark = new(118, 59, 54, 255);
    public static readonly Color Shadow = new(0, 0, 0, 80);
    public static readonly Color GhostTint = new(255, 255, 255, 180);    // призрак полупрозрачный
    public static readonly Color Me = new(255, 214, 90, 255);            // своё кольцо и свой ник
    public static readonly Color Beam = new(255, 236, 160, 40);          // луч глазами охотника
    public static readonly Color DangerBeam = new(255, 70, 70, 70);      // луч глазами призрака — опасная зона
    public static readonly Color ItemGlow = new(255, 200, 110, 130);
    public static readonly Color CurseGlow = new(190, 80, 255, 160);
    public static readonly Color Plate = new(20, 13, 20, 190);           // подложка под ником
    public static readonly Color Overlay = new(14, 9, 14, 200);          // затемнение карты под окном «Время вышло»
}
