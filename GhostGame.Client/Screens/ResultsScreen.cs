using GhostGame.Client.Graphics;
using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client.Screens;

// Экран итогов раунда: победитель, причина, игроки и их роли, кнопка «В комнату».
// Только рисует то, что пришло в GameOver. Возвращает true, когда нажали «В комнату».
public sealed class ResultsScreen
{
    // nicks — playerId → ник, запомненные на старте раунда: отключившегося уже нет в RoomState, а в итогах он нужен
    public bool Draw(Role winner, GameOverReason reason, IReadOnlyList<PlayerRole> players,
        IReadOnlyDictionary<int, string> nicks, int myId)
    {
        Ui.Backdrop();
        var card = Ui.Center(620, 680);
        Ui.Panel(card);
        var centerX = card.X + card.Width / 2;

        // кто победил — картинкой: парящий призрак или охотники в ряд
        if (winner == Role.Ghost)
        {
            var hover = MathF.Sin((float)Raylib.GetTime() * 2) * 6;
            Assets.DrawSprite(Sprites.Ghost, new Rectangle(centerX - 48, card.Y + 26 + hover, 96, 96), Theme.GhostTint);
        }
        else
        {
            var hunters = players.Where(p => p.Role == Role.Hunter).ToList();
            for (var i = 0; i < hunters.Count; i++)
            {
                var x = centerX - hunters.Count * 40 + i * 80;
                Assets.DrawSprite(Sprites.Hunter(hunters[i].PlayerId), new Rectangle(x, card.Y + 40, 80, 80));
            }
        }

        Ui.TextCentered(winner == Role.Ghost ? "Победил призрак" : "Победили охотники", centerX, card.Y + 136, 40);
        Ui.TextCentered(ReasonText(reason), centerX, card.Y + 186, 20, Theme.Muted);

        var me = players.FirstOrDefault(p => p.PlayerId == myId);
        if (me != null)
        {
            var won = me.Role == winner;
            Ui.TextCentered(won ? "Вы победили!" : "Вы проиграли", centerX, card.Y + 218, 24, won ? Theme.Good : Theme.Error);
        }

        // кто кем был: призрак первым, потом охотники
        var y = card.Y + 268;
        foreach (var p in players.OrderByDescending(p => p.Role))
        {
            var row = new Rectangle(card.X + 40, y, card.Width - 80, 54);
            var isMe = p.PlayerId == myId;
            Ui.Row(row, enabled: false, highlighted: isMe);

            var sprite = new Rectangle(row.X + 10, row.Y + 7, 40, 40);
            if (p.Role == Role.Ghost)
            {
                Assets.DrawSprite(Sprites.Ghost, sprite, Theme.GhostTint);
            }
            else
            {
                Assets.DrawSprite(Sprites.Hunter(p.PlayerId), sprite);
            }

            var nick = nicks.TryGetValue(p.PlayerId, out var n) ? n : $"#{p.PlayerId}";
            Ui.Text(isMe ? $"{nick} (вы)" : nick, row.X + 62, row.Y + 14, 22, isMe ? Theme.Me : Theme.Text);
            Ui.Chip(p.Role == Role.Ghost ? "призрак" : "охотник", row.X + row.Width - 12, row.Y + 13,
                p.Role == Role.Ghost ? Theme.Stone : Theme.Accent);
            y += 62;
        }

        return Ui.Button(new Rectangle(card.X + 40, card.Y + card.Height - 84, card.Width - 80, 58), "В комнату", 24, primary: true);
    }

    private static string ReasonText(GameOverReason reason) => reason switch
    {
        GameOverReason.GhostCaught => "призрак попался в лучи фонарей",
        GameOverReason.Cursed => "призрак проклял 3 предмета",
        GameOverReason.HuntersLeft => "все охотники отключились",
        GameOverReason.TimeUp => "время вышло — призрак продержался",
        GameOverReason.GhostLeft => "призрак отключился",
        _ => "",
    };
}
