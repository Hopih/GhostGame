using GhostGame.Client.Graphics;
using Raylib_cs;

namespace GhostGame.Client.Screens;

public sealed class ConnectScreen
{
    private readonly TextInput nickInput = new(16, focused: true);
    private readonly TextInput serverInput = new(64);

    public ConnectScreen(string server)
    {
        serverInput.Text = server;
    }

    public string Nick => nickInput.Text.Trim();

    public string Server => serverInput.Text.Trim();

    public bool Draw(bool waiting)
    {
        Ui.Backdrop();
        var centerX = Raylib.GetScreenWidth() / 2f;
        var top = MathF.Max(40, Raylib.GetScreenHeight() * 0.14f);
        var t = (float)Raylib.GetTime();

        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            var toServer = nickInput.Focused;
            nickInput.Focused = !toServer;
            serverInput.Focused = toServer;
        }

        Raylib.DrawCircleGradient((int)centerX, (int)(top + 220), 640, new Color(10, 6, 12, 150), new Color(10, 6, 12, 0));
        Ui.Title("Призрак и охотники", centerX, top, 80);
        Ui.TextCentered("сетевая игра на 3–5 игроков  ·  один из вас — призрак", centerX, top + 112, 22, Theme.Text);

        var card = new Rectangle(centerX - 260, top + 176, 520, 380);
        Ui.Panel(card);
        Ui.Text("Ваш ник", card.X + 40, card.Y + 30, 18, Theme.Muted);
        var enter = nickInput.Draw(new Rectangle(card.X + 40, card.Y + 56, card.Width - 80, 56), "3–16 символов", 24);
        Ui.Text("Сервер", card.X + 40, card.Y + 128, 18, Theme.Muted);
        enter |= serverInput.Draw(new Rectangle(card.X + 40, card.Y + 154, card.Width - 80, 56), "адрес:порт", 24);
        var click = Ui.Button(new Rectangle(card.X + 40, card.Y + 236, card.Width - 80, 60),
            waiting ? "Подключение…" : "Войти", 26, enabled: !waiting, primary: true);
        Ui.TextCentered("Tab — между полями   ·   свой сервер на этом компьютере — 127.0.0.1:5000",
            centerX, card.Y + 314, 16, Theme.Muted);
        Ui.TextCentered("WASD — ходить   ·   мышь — куда светить   ·   ЛКМ — фонарь или проклятие",
            centerX, card.Y + 342, 16, Theme.Muted);

        var side = 150f;
        var hunter = new Rectangle(card.X - side - 40, card.Y + 80, side, side);
        Raylib.DrawEllipse((int)(hunter.X + side / 2), (int)(hunter.Y + side * 0.95f), side * 0.32f, side * 0.08f, Theme.Shadow);
        Assets.DrawSprite(Sprites.Hunter(1), hunter);

        var ghost = new Rectangle(card.X + card.Width + 40, card.Y + 60 + MathF.Sin(t * 2) * 10, side, side);
        Raylib.DrawCircleGradient((int)(ghost.X + side / 2), (int)(ghost.Y + side / 2), side * 0.8f,
            Theme.GhostGlow, Raylib.Fade(Theme.GhostGlow, 0));
        Assets.DrawSprite(Sprites.Ghost, ghost, Theme.GhostTint, flip: true);

        return (enter || click) && !waiting && Nick.Length > 0 && Server.Length > 0;
    }
}
