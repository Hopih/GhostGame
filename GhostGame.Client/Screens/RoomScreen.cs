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
    private const int MaxPlayers = 5;
    private const float ChatLine = 28;

    private readonly TextInput chatInput = new(200);

    public void Focus() => chatInput.Focused = true;

    // players и hostId — из последнего RoomState: готовность показываем такой, какой её считает сервер (ТЗ 3.4)
    public RoomAction? Draw(string roomName, int hostId, IReadOnlyList<RoomPlayer> players, int myId, IReadOnlyList<string> chat)
    {
        RoomAction? action = null;

        Ui.Backdrop();
        var frame = Ui.Center(1180, 700);
        Ui.Text($"Комната «{roomName}»", frame.X, frame.Y, 44);
        Ui.Text("Раунд начнётся, когда в комнате 3–5 игроков и все готовы", frame.X, frame.Y + 56, 20, Theme.Muted);

        // игроки
        var left = new Rectangle(frame.X, frame.Y + 100, 500, 600);
        Ui.Panel(left);
        Ui.Text($"Игроки  {players.Count}/{MaxPlayers}", left.X + 24, left.Y + 18, 26);
        for (var i = 0; i < MaxPlayers; i++)
        {
            var row = new Rectangle(left.X + 20, left.Y + 70 + i * 72, left.Width - 40, 62);
            if (i >= players.Count)
            {
                Raylib.DrawRectangleRoundedLinesEx(row, 0.3f, 8, 2, Theme.PanelBorder); // свободное место — пустая рамка
                Ui.Text("свободно", row.X + 70, row.Y + 20, 20, Raylib.Fade(Theme.Muted, 0.5f));
                continue;
            }

            var p = players[i];
            var isMe = p.PlayerId == myId;
            Ui.Row(row, enabled: false, highlighted: isMe);
            Assets.DrawSprite(Sprites.Hunter(p.PlayerId), new Rectangle(row.X + 10, row.Y + 7, 48, 48));
            Ui.Text(p.Nick, row.X + 70, row.Y + 8, 24, isMe ? Theme.Me : Theme.Text);
            var tags = string.Join("  ·  ", new[] { p.PlayerId == hostId ? "хост" : "", isMe ? "вы" : "" }.Where(t => t.Length > 0));
            Ui.Text(tags, row.X + 70, row.Y + 36, 16, Theme.Muted);
            Ui.Chip(p.Ready ? "готов" : "не готов", row.X + row.Width - 12, row.Y + 17, p.Ready ? Theme.Good : Theme.Muted);
        }

        var meReady = players.FirstOrDefault(p => p.PlayerId == myId)?.Ready ?? false;
        var buttonsY = left.Y + left.Height - 82;
        if (Ui.Button(new Rectangle(left.X + 20, buttonsY, 220, 58), meReady ? "Не готов" : "Готов", 24, primary: !meReady))
        {
            action = new RoomAction.ToggleReady();
        }
        if (Ui.Button(new Rectangle(left.X + left.Width - 240, buttonsY, 220, 58), "Выйти", 24))
        {
            action = new RoomAction.Leave();
        }

        // чат
        var right = new Rectangle(frame.X + 530, frame.Y + 100, 650, 600);
        Ui.Panel(right);
        Ui.Text("Чат", right.X + 24, right.Y + 18, 26);
        var area = new Rectangle(right.X + 20, right.Y + 64, right.Width - 40, right.Height - 64 - 92);
        Raylib.DrawRectangleRounded(area, 0.04f, 8, Theme.Field);
        Raylib.BeginScissorMode((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height); // длинные строки не вылезают за окно чата
        var lines = chat.TakeLast((int)((area.Height - 20) / ChatLine)).ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            DrawChatLine(lines[i], area.X + 14, area.Y + 10 + i * ChatLine);
        }
        Raylib.EndScissorMode();

        var inputY = right.Y + right.Height - 76;
        var enter = chatInput.Draw(new Rectangle(right.X + 20, inputY, right.Width - 200, 54), "сообщение, Enter — отправить");
        var click = Ui.Button(new Rectangle(right.X + right.Width - 168, inputY, 148, 54), "Отправить", 20);
        if ((enter || click) && chatInput.Text.Trim().Length > 0)
        {
            action = new RoomAction.Chat(chatInput.Text.Trim());
            chatInput.Text = "";
        }

        return action;
    }

    // Строка чата хранится как «ник: текст» — ник выделяем цветом
    private static void DrawChatLine(string line, float x, float y)
    {
        var colon = line.IndexOf(": ", StringComparison.Ordinal);
        if (colon < 0)
        {
            Ui.Text(line, x, y, 20);
            return;
        }
        var author = line[..(colon + 1)];
        Ui.Text(author, x, y, 20, Theme.Accent);
        Ui.Text(line[(colon + 2)..], x + Ui.MeasureText(author + " ", 20), y, 20);
    }
}
