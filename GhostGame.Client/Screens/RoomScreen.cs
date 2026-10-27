using GhostGame.Client.Graphics;
using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client.Screens;

// Что игрок сделал в комнате в этом кадре. Program.cs превращает это в пакеты.
public abstract record RoomAction
{
    public sealed record ToggleReady() : RoomAction;  // «Готов» / «Не готов»
    public sealed record Leave() : RoomAction;        // «Выйти»
    public sealed record Chat(string Text) : RoomAction;
}

// Экран комнаты: игроки, готовность, чат. Только рисует и сообщает, что нажали.
public sealed class RoomScreen
{
    private readonly TextInput chatInput = new(200);

    public void Focus() => chatInput.Focused = true;

    // players и hostId — из последнего RoomState: готовность показываем такой, какой её считает сервер (ТЗ 3.4)
    public RoomAction? Draw(string roomName, int hostId, IReadOnlyList<RoomPlayer> players, int myId, IReadOnlyList<string> chat)
    {
        RoomAction? action = null;

        Ui.Text($"Комната «{roomName}»", 40, 30, 40);
        Ui.Text("Раунд начнётся, когда в комнате 3–5 игроков и все готовы", 40, 85, 20, Theme.Muted);

        for (var i = 0; i < players.Count; i++)
        {
            var p = players[i];
            var y = 150 + i * 44;
            var tags = (p.PlayerId == hostId ? "  (хост)" : "") + (p.PlayerId == myId ? "  (вы)" : "");
            Ui.Text(p.Nick + tags, 60, y, 24);
            Ui.Text(p.Ready ? "готов" : "не готов", 400, y, 24, p.Ready ? Theme.Accent : Theme.Muted);
        }

        var meReady = players.FirstOrDefault(p => p.PlayerId == myId)?.Ready ?? false;
        if (Ui.Button(new Rectangle(40, 400, 220, 50), meReady ? "Не готов" : "Готов"))
        {
            action = new RoomAction.ToggleReady();
        }
        if (Ui.Button(new Rectangle(280, 400, 220, 50), "Выйти"))
        {
            action = new RoomAction.Leave();
        }

        Ui.Text("Чат комнаты", 640, 140, 24);
        Raylib.DrawRectangle(640, 180, 600, 480, Theme.Panel);
        Raylib.BeginScissorMode(640, 180, 600, 480); // длинные строки не вылезают за панель
        var lines = chat.TakeLast(18).ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            Ui.Text(lines[i], 652, 190 + i * 25, 20);
        }
        Raylib.EndScissorMode();

        var enter = chatInput.Draw(new Rectangle(640, 676, 460, 44), "сообщение, Enter — отправить");
        var click = Ui.Button(new Rectangle(1110, 676, 130, 44), "Отправить", 20);
        if ((enter || click) && chatInput.Text.Trim().Length > 0)
        {
            action = new RoomAction.Chat(chatInput.Text.Trim());
            chatInput.Text = "";
        }

        return action;
    }
}
