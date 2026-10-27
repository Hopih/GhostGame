using GhostGame.Client.Graphics;
using Raylib_cs;

namespace GhostGame.Client.Screens;

// Экран подключения: поле ника и кнопка «Войти».
// Только рисует и читает нажатия, в сеть ничего не отправляет — что делать с ником, решает Program.cs.
public sealed class ConnectScreen
{
    private readonly TextInput nickInput = new(16, focused: true);

    public string Nick => nickInput.Text.Trim();

    // waiting — Hello уже отправлен, ждём Welcome: кнопка серая.
    // Возвращает ник, если в этом кадре нажали «Войти» или Enter, иначе null.
    public string? Draw(string server, bool waiting)
    {
        Ui.Backdrop();
        var card = Ui.Center(520, 460);
        Ui.Panel(card);
        var centerX = card.X + card.Width / 2;

        // картинка: призрак парит между двумя охотниками
        var hover = MathF.Sin((float)Raylib.GetTime() * 2) * 8;
        Assets.DrawSprite(Sprites.Hunter(1), new Rectangle(centerX - 150, card.Y + 50, 80, 80));
        Assets.DrawSprite(Sprites.Ghost, new Rectangle(centerX - 48, card.Y + 26 + hover, 96, 96), Theme.GhostTint);
        Assets.DrawSprite(Sprites.Hunter(2), new Rectangle(centerX + 70, card.Y + 50, 80, 80), flip: true);

        Ui.TextCentered("Призрак и охотники", centerX, card.Y + 148, 40);
        Ui.TextCentered($"сервер {server}", centerX, card.Y + 198, 18, Theme.Muted);

        Ui.Text("Ник", card.X + 50, card.Y + 246, 18, Theme.Muted);
        var enter = nickInput.Draw(new Rectangle(card.X + 50, card.Y + 272, card.Width - 100, 52), "3–16 символов", 24);
        var click = Ui.Button(new Rectangle(card.X + 50, card.Y + 352, card.Width - 100, 58),
            waiting ? "Подключение…" : "Войти", 26, enabled: !waiting, primary: true);
        return (enter || click) && !waiting && Nick.Length > 0 ? Nick : null;
    }
}
