using GhostGame.Client.Graphics;
using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client.Screens;

// Что игрок сделал в лобби в этом кадре. Program.cs превращает это в пакеты.
public abstract record LobbyAction
{
    public sealed record Join(int RoomId) : LobbyAction;     // клик по комнате
    public sealed record Create(string Name) : LobbyAction;  // «Создать»
    public sealed record Refresh() : LobbyAction;            // «Обновить»
}

// Экран лобби: список комнат и создание своей. Только рисует и сообщает, что нажали.
public sealed class LobbyScreen
{
    private readonly TextInput roomNameInput = new(24);

    public void Focus() => roomNameInput.Focused = true;

    // Возвращает действие игрока или null, если в этом кадре ничего не нажали
    public LobbyAction? Draw(IReadOnlyList<RoomInfo> rooms, string nick, int myId, long pingMs)
    {
        LobbyAction? action = null; // рисуем экран целиком, даже если кнопку уже нажали, и только потом отвечаем

        Ui.Text("Лобби", 40, 30, 40);
        Ui.Text($"Вы: {nick} (id {myId})   ·   пинг: {(pingMs < 0 ? "—" : $"{pingMs} мс")}", 40, 85, 20, Theme.Muted);

        Ui.Text("Комнаты — клик, чтобы войти:", 40, 140, 24);
        if (rooms.Count == 0)
        {
            Ui.Text("Комнат пока нет — создайте свою справа", 40, 185, 20, Theme.Muted);
        }
        for (var i = 0; i < rooms.Count; i++)
        {
            var r = rooms[i];
            var full = r.Players >= r.MaxPlayers;
            var note = r.InGame ? "   — идёт игра" : full ? "   — заполнена" : "";
            if (Ui.Button(new Rectangle(40, 180 + i * 56, 640, 46), $"{r.Name}   {r.Players}/{r.MaxPlayers}{note}", 22, !r.InGame && !full))
            {
                action = new LobbyAction.Join(r.RoomId);
            }
        }
        if (Ui.Button(new Rectangle(40, 650, 200, 46), "Обновить"))
        {
            action = new LobbyAction.Refresh();
        }

        Ui.Text("Новая комната:", 760, 140, 24);
        var enter = roomNameInput.Draw(new Rectangle(760, 180, 440, 44), "название, 1–24 символа");
        var click = Ui.Button(new Rectangle(760, 236, 200, 46), "Создать");
        if ((enter || click) && roomNameInput.Text.Trim().Length > 0)
        {
            action = new LobbyAction.Create(roomNameInput.Text.Trim());
            roomNameInput.Text = "";
        }

        return action;
    }
}
