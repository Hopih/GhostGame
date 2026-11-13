using GhostGame.Client.Graphics;
using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client.Screens;

public abstract record LobbyAction
{
    public sealed record Join(int RoomId) : LobbyAction;
    public sealed record Create(string Name) : LobbyAction;
    public sealed record Refresh() : LobbyAction;
}

public sealed class LobbyScreen
{
    private readonly TextInput roomNameInput = new(24);

    public void Focus() => roomNameInput.Focused = true;

    public LobbyAction? Draw(IReadOnlyList<RoomInfo> rooms, string nick, int myId, long pingMs)
    {
        LobbyAction? action = null;

        Ui.Backdrop();
        var frame = Ui.Center(1180, 700);

        Ui.Text("Лобби", frame.X, frame.Y, 48);
        var me = $"{nick}   ·   id {myId}   ·   пинг {(pingMs < 0 ? "—" : $"{pingMs} мс")}";
        var meX = frame.X + frame.Width - Ui.MeasureText(me, 20);
        Ui.Text(me, meX, frame.Y + 20, 20, Theme.Muted);
        Assets.DrawSprite(Sprites.Hunter(myId), new Rectangle(meX - 50, frame.Y + 8, 40, 40));

        var list = new Rectangle(frame.X, frame.Y + 80, 740, 620);
        Ui.Panel(list);
        Ui.Text("Комнаты", list.X + 24, list.Y + 18, 28);
        if (Ui.Button(new Rectangle(list.X + list.Width - 184, list.Y + 14, 160, 44), "Обновить", 20))
        {
            action = new LobbyAction.Refresh();
        }
        if (rooms.Count == 0)
        {
            Ui.TextCentered("Комнат пока нет — создайте свою справа", list.X + list.Width / 2, list.Y + 280, 22, Theme.Muted);
        }
        for (var i = 0; i < rooms.Count; i++)
        {
            var r = rooms[i];
            var full = r.Players >= r.MaxPlayers;
            var open = !r.InGame && !full;
            var row = new Rectangle(list.X + 20, list.Y + 76 + i * 64, list.Width - 40, 56);
            if (Ui.Row(row, open))
            {
                action = new LobbyAction.Join(r.RoomId);
            }
            Ui.Text(r.Name, row.X + 18, row.Y + 14, 24, open ? Theme.Text : Theme.Muted);

            for (var k = 0; k < r.MaxPlayers; k++)
            {
                var slot = new Rectangle(row.X + 330 + k * 30, row.Y + 12, 32, 32);
                Assets.DrawSprite(Sprites.Hunter(k + 1), slot, k < r.Players ? Color.White : Raylib.Fade(Color.White, 0.15f));
            }
            Ui.Text($"{r.Players}/{r.MaxPlayers}", row.X + 490, row.Y + 16, 20, Theme.Muted);

            var (label, color) = r.InGame ? ("идёт игра", Theme.Error) : full ? ("заполнена", Theme.Muted) : ("войти →", Theme.Good);
            Ui.Chip(label, row.X + row.Width - 14, row.Y + 13, color);
        }

        var create = new Rectangle(frame.X + 770, frame.Y + 80, 410, 230);
        Ui.Panel(create);
        Ui.Text("Новая комната", create.X + 24, create.Y + 18, 28);
        var enter = roomNameInput.Draw(new Rectangle(create.X + 24, create.Y + 74, create.Width - 48, 52), "название, 1–24 символа");
        var click = Ui.Button(new Rectangle(create.X + 24, create.Y + 148, create.Width - 48, 56), "Создать", 24, primary: true);
        if ((enter || click) && roomNameInput.Text.Trim().Length > 0)
        {
            action = new LobbyAction.Create(roomNameInput.Text.Trim());
            roomNameInput.Text = "";
        }

        var rules = new Rectangle(frame.X + 770, frame.Y + 330, 410, 370);
        Ui.Panel(rules);
        Ui.Text("Как играть", rules.X + 24, rules.Y + 18, 28);
        var y = rules.Y + 70;
        Assets.DrawSprite(Sprites.Ghost, new Rectangle(rules.X + 24, y, 48, 48), Theme.GhostTint);
        y += Ui.Paragraph("Призрак проклинает сундуки: рядом с сундуком держать ЛКМ 3 с, можно с перерывами. Проклял 3 из 5 или продержался до конца раунда — победа.",
            rules.X + 86, y, rules.Width - 110, 18, Theme.Text) + 14;
        Assets.DrawSprite(Sprites.Hunter(3), new Rectangle(rules.X + 24, y, 48, 48));
        y += Ui.Paragraph("Охотники ищут его в темноте: 3 с в лучах фонарей — призрак пойман. За шкафами и колоннами луч не достаёт.",
            rules.X + 86, y, rules.Width - 110, 18, Theme.Text) + 14;
        Ui.Paragraph("В комнате 3–5 игроков, раунд — 4 минуты.", rules.X + 24, y + 10, rules.Width - 48, 18, Theme.Muted);

        return action;
    }
}
