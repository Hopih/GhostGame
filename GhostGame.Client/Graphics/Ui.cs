using System.Numerics;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Простейший интерфейс поверх Raylib-cs: шрифт с кириллицей, текст, кнопка, поле ввода.
// Всё рисуется заново каждый кадр между BeginDrawing и EndDrawing. Поэтому
// if (Ui.Button(...)) { ... } одновременно рисует кнопку и сообщает, что по ней кликнули в этом кадре.
public static class Ui
{
    private const float Spacing = 1f;

    public static Font Font { get; private set; }

    // Стандартный шрифт Raylib знает только латиницу — русский текст был бы «?».
    // Поэтому грузим системный TTF и просим в нём латиницу, кириллицу и несколько знаков.
    // Вызывать один раз после InitWindow.
    public static void LoadFont(int baseSize = 32)
    {
        var codepoints = Enumerable.Range(32, 95)                 // пробел, латиница, цифры, знаки
            .Concat(Enumerable.Range(0x400, 0x60))               // кириллица: А–я, Ё, ё
            .Concat("«»—–…№·".Select(c => (int)c))
            .ToArray();

        foreach (var path in new[] { @"C:\Windows\Fonts\segoeui.ttf", @"C:\Windows\Fonts\arial.ttf", @"C:\Windows\Fonts\tahoma.ttf" })
        {
            if (File.Exists(path))
            {
                Font = Raylib.LoadFontEx(path, baseSize, codepoints, codepoints.Length);
                Raylib.SetTextureFilter(Font.Texture, TextureFilter.Bilinear);
                return;
            }
        }
        Font = Raylib.GetFontDefault(); // системного шрифта нет — будет только латиница
    }

    public static void Text(string text, float x, float y, float size = 22, Color? color = null)
    {
        Raylib.DrawTextEx(Font, text, new Vector2(x, y), size, Spacing, color ?? Theme.Text);
    }

    public static float MeasureText(string text, float size = 22)
    {
        return Raylib.MeasureTextEx(Font, text, size, Spacing).X;
    }

    // Кнопка: прямоугольник с подписью по центру. true — по ней кликнули в этом кадре.
    // enabled = false — кнопка серая и не нажимается.
    public static bool Button(Rectangle bounds, string label, float size = 22, bool enabled = true)
    {
        var hover = enabled && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), bounds);
        Raylib.DrawRectangleRec(bounds, hover ? Theme.PanelHover : Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 2, enabled ? Theme.Accent : Theme.Muted);
        var textWidth = MeasureText(label, size);
        Text(label, bounds.X + (bounds.Width - textWidth) / 2, bounds.Y + (bounds.Height - size) / 2, size,
            enabled ? Theme.Text : Theme.Muted);
        return hover && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    // Строка статуса внизу экрана: ошибки красным, остальное серым
    public static void StatusLine(string text, bool isError)
    {
        Text(text, 40, 750, 22, isError ? Theme.Error : Theme.Muted);
    }
}

// Поле ввода: клик — поле получает фокус, печатать с клавиатуры (кириллица тоже), Backspace стирает, Enter — «готово».
// Экземпляр живёт между кадрами (в нём хранится набранный текст), Draw вызывается каждый кадр.
public sealed class TextInput
{
    public TextInput(int maxLength, bool focused = false)
    {
        MaxLength = maxLength;
        Focused = focused;
    }

    public int MaxLength { get; }

    public string Text { get; set; } = "";

    public bool Focused { get; set; }

    // Рисует поле и обрабатывает ввод. Возвращает true в том кадре, когда нажали Enter и текст не пустой.
    public bool Draw(Rectangle bounds, string placeholder = "", float size = 22)
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Focused = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), bounds);
        }

        var submitted = false;
        if (Focused)
        {
            // GetCharPressed отдаёт по одному символу, набранному за кадр, и 0, когда символы кончились
            var ch = Raylib.GetCharPressed();
            while (ch > 0)
            {
                if (ch >= 32 && Text.Length < MaxLength)
                {
                    Text += char.ConvertFromUtf32(ch);
                }
                ch = Raylib.GetCharPressed();
            }

            // IsKeyPressedRepeat — чтобы зажатый Backspace стирал несколько символов
            if ((Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace)) && Text.Length > 0)
            {
                Text = Text[..^1];
            }

            if ((Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter)) && Text.Trim().Length > 0)
            {
                submitted = true;
            }
        }

        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 2, Focused ? Theme.Accent : Theme.Muted);

        var textY = bounds.Y + (bounds.Height - size) / 2;
        if (Text.Length == 0 && !Focused)
        {
            Ui.Text(placeholder, bounds.X + 10, textY, size, Theme.Muted);
        }
        else
        {
            // мигающий курсор; длинный текст сдвигаем влево, чтобы был виден конец
            var shown = Text + (Focused && Raylib.GetTime() % 1 < 0.5 ? "|" : "");
            var overflow = MathF.Max(0, Ui.MeasureText(shown, size) - (bounds.Width - 20));
            Raylib.BeginScissorMode((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
            Ui.Text(shown, bounds.X + 10 - overflow, textY, size);
            Raylib.EndScissorMode();
        }
        return submitted;
    }
}
