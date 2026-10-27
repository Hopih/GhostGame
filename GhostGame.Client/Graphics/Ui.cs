using System.Numerics;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Простейший интерфейс поверх Raylib-cs: текст, панели, кнопки, строки списка, поле ввода.
// Всё рисуется заново каждый кадр между BeginDrawing и EndDrawing. Поэтому
// if (Ui.Button(...)) { ... } одновременно рисует кнопку и сообщает, что по ней кликнули в этом кадре.
public static class Ui
{
    private const float Spacing = 1f;

    public static void Text(string text, float x, float y, float size = 22, Color? color = null)
    {
        Raylib.DrawTextEx(Assets.Font, text, new Vector2(x, y), size, Spacing, color ?? Theme.Text);
    }

    public static void TextCentered(string text, float centerX, float y, float size = 22, Color? color = null)
    {
        Text(text, centerX - MeasureText(text, size) / 2, y, size, color);
    }

    public static float MeasureText(string text, float size = 22)
    {
        return Raylib.MeasureTextEx(Assets.Font, text, size, Spacing).X;
    }

    // Текст с переносом по словам в ширину width. Возвращает высоту, которую он занял.
    public static float Paragraph(string text, float x, float y, float width, float size, Color color)
    {
        var top = y;
        var line = "";
        foreach (var word in text.Split(' '))
        {
            var longer = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && MeasureText(longer, size) > width)
            {
                Text(line, x, y, size, color);
                y += size * 1.3f;
                line = word;
            }
            else
            {
                line = longer;
            }
        }
        if (line.Length > 0)
        {
            Text(line, x, y, size, color);
            y += size * 1.3f;
        }
        return y - top;
    }

    // Прямоугольник width × height по центру окна — в нём раскладываются меню
    public static Rectangle Center(float width, float height)
    {
        return new Rectangle((Raylib.GetScreenWidth() - width) / 2, (Raylib.GetScreenHeight() - height) / 2, width, height);
    }

    // Фон меню: пол подземелья крупными плитками под тёмной вуалью
    public static void Backdrop()
    {
        const int tile = Assets.SpriteSize * 4;
        for (var y = 0; y * tile < Raylib.GetScreenHeight(); y++)
        {
            for (var x = 0; x * tile < Raylib.GetScreenWidth(); x++)
            {
                var sprite = (x * 7 + y * 13) % 9 == 0 ? Sprites.FloorDots : Sprites.Floor;
                Assets.DrawSprite(sprite, new Rectangle(x * tile, y * tile, tile, tile));
            }
        }
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Veil);
    }

    // Карточка-подложка: тень, скруглённые углы, рамка
    public static void Panel(Rectangle bounds)
    {
        var round = Roundness(bounds, 14);
        Raylib.DrawRectangleRounded(bounds with { X = bounds.X + 4, Y = bounds.Y + 6 }, round, 8, Theme.Shadow);
        Raylib.DrawRectangleRounded(bounds, round, 8, Theme.Panel);
        Raylib.DrawRectangleRoundedLinesEx(bounds, round, 8, 2, Theme.PanelBorder);
    }

    // Кнопка: true — по ней кликнули в этом кадре. enabled = false — серая и не нажимается.
    // primary — главная кнопка экрана (песочная), остальные — кирпичные.
    public static bool Button(Rectangle bounds, string label, float size = 22, bool enabled = true, bool primary = false)
    {
        var hover = enabled && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), bounds);
        var pressed = hover && Raylib.IsMouseButtonDown(MouseButton.Left);
        var round = Roundness(bounds, 10);

        // «толщина» кнопки — тёмная полоса снизу; нажатая кнопка опускается на неё
        Raylib.DrawRectangleRounded(bounds with { Y = bounds.Y + 4 }, round, 8, Theme.Outline);
        var face = pressed ? bounds with { Y = bounds.Y + 3 } : bounds;
        var fill = !enabled ? Theme.ButtonDisabled
            : primary ? (hover ? Theme.AccentHover : Theme.Accent)
            : hover ? Theme.ButtonHover : Theme.Button;
        Raylib.DrawRectangleRounded(face, round, 8, fill);
        Raylib.DrawRectangleRoundedLinesEx(face, round, 8, 2, Theme.Outline);

        var textColor = !enabled ? Theme.Muted : primary ? Theme.Outline : Theme.Text;
        TextCentered(label, face.X + face.Width / 2, face.Y + (face.Height - size) / 2 - 1, size, textColor);
        return hover && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    // Строка списка, по которой можно кликнуть. Рисует только подложку, содержимое рисует вызывающий.
    public static bool Row(Rectangle bounds, bool enabled, bool highlighted = false)
    {
        var hover = enabled && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), bounds);
        Raylib.DrawRectangleRounded(bounds, Roundness(bounds, 10), 8, hover || highlighted ? Theme.RowHover : Theme.Row);
        return hover && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    // Маленькая цветная метка («готов», «идёт игра»). rightX — где должен кончаться её правый край.
    public static void Chip(string text, float rightX, float y, Color color, float size = 18)
    {
        var width = MeasureText(text, size) + 20;
        var bounds = new Rectangle(rightX - width, y, width, size + 10);
        Raylib.DrawRectangleRounded(bounds, 1, 8, Raylib.Fade(color, 0.18f));
        Text(text, bounds.X + 10, bounds.Y + 5, size, color);
    }

    // Строка статуса внизу окна: ошибки красным, остальное серым
    public static void StatusLine(string text, bool isError)
    {
        const float size = 22;
        var bounds = new Rectangle(20, Raylib.GetScreenHeight() - 58, MeasureText(text, size) + 32, 40);
        Raylib.DrawRectangleRounded(bounds, 0.5f, 8, Theme.Plate);
        Text(text, bounds.X + 16, bounds.Y + 8, size, isError ? Theme.Error : Theme.Text);
    }

    // Raylib задаёт скругление долей от меньшей стороны; переводим из «радиус в пикселях»
    private static float Roundness(Rectangle bounds, float radius)
    {
        return Math.Min(1f, 2 * radius / Math.Min(bounds.Width, bounds.Height));
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

        var round = Math.Min(1f, 20 / Math.Min(bounds.Width, bounds.Height));
        Raylib.DrawRectangleRounded(bounds, round, 8, Theme.Field);
        Raylib.DrawRectangleRoundedLinesEx(bounds, round, 8, 2, Focused ? Theme.Accent : Theme.PanelBorder);

        var textY = bounds.Y + (bounds.Height - size) / 2;
        if (Text.Length == 0 && !Focused)
        {
            Ui.Text(placeholder, bounds.X + 14, textY, size, Theme.Muted);
        }
        else
        {
            // мигающий курсор; длинный текст сдвигаем влево, чтобы был виден конец
            var shown = Text + (Focused && Raylib.GetTime() % 1 < 0.5 ? "|" : "");
            var overflow = MathF.Max(0, Ui.MeasureText(shown, size) - (bounds.Width - 28));
            Raylib.BeginScissorMode((int)bounds.X + 4, (int)bounds.Y, (int)bounds.Width - 8, (int)bounds.Height);
            Ui.Text(shown, bounds.X + 14 - overflow, textY, size);
            Raylib.EndScissorMode();
        }
        return submitted;
    }
}
