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
        Ui.Text("Призрак и охотники", 440, 200, 48);
        Ui.Text($"Сервер: {server}", 440, 270, 20, Theme.Muted);
        Ui.Text("Ник:", 440, 332);
        var enter = nickInput.Draw(new Rectangle(500, 320, 320, 44), "3–16 символов");
        var click = Ui.Button(new Rectangle(500, 380, 320, 48), waiting ? "Подключение…" : "Войти", enabled: !waiting);
        return (enter || click) && !waiting && Nick.Length > 0 ? Nick : null;
    }
}
