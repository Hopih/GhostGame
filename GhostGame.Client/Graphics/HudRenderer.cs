using System.Numerics;
using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Панель справа от карты: роль, таймер, проклятые предметы, игроки, пинг, подсказка по управлению
public static class HudRenderer
{
    public static void Draw(GameLayout layout, Role myRole, int myId, ushort timeLeft, byte curseMask,
        IReadOnlyList<RosterEntry> roster, long pingMs)
    {
        var panel = layout.Panel;
        Ui.Panel(panel);
        var x = panel.X + 16;
        var y = panel.Y + 16;
        var width = panel.Width - 32;

        // роль
        if (myRole == Role.Ghost)
        {
            Assets.DrawSprite(Sprites.Ghost, new Rectangle(x, y, 40, 40), Theme.GhostTint);
        }
        else
        {
            Assets.DrawSprite(Sprites.Hunter(myId), new Rectangle(x, y, 40, 40));
        }
        Ui.Text(myRole == Role.Ghost ? "Вы — призрак" : "Вы — охотник", x + 50, y + 8, 22);
        y += 60;

        // таймер; последние 30 секунд — красным
        Ui.Text("ВРЕМЯ", x, y, 16, Theme.Muted);
        Ui.Text($"{timeLeft / 60}:{timeLeft % 60:00}", x, y + 16, 52, timeLeft <= 30 ? Theme.Error : Theme.Text);
        y += 84;

        // проклятые предметы: сундук — цел, мимик — проклят; призраку для победы нужно 3 (ТЗ 6)
        Ui.Text($"ПРОКЛЯТО  {BitOperations.PopCount(curseMask)} / 3", x, y, 16, Theme.Muted);
        var icon = MathF.Min(40, (width - 4 * 6) / 5);
        for (var i = 0; i < 5; i++)
        {
            var cursed = (curseMask & (1 << i)) != 0;
            Assets.DrawSprite(cursed ? Sprites.Mimic : Sprites.Chest, new Rectangle(x + i * (icon + 6), y + 22, icon, icon));
        }
        y += 22 + icon + 20;

        // игроки раунда
        Ui.Text("ИГРОКИ", x, y, 16, Theme.Muted);
        y += 22;
        foreach (var entry in roster)
        {
            var sprite = new Rectangle(x, y, 32, 32);
            if (entry.IsGhost)
            {
                Assets.DrawSprite(Sprites.Ghost, sprite, Theme.GhostTint);
            }
            else
            {
                Assets.DrawSprite(Sprites.Hunter(entry.PlayerId), sprite);
            }
            var isMe = entry.PlayerId == myId;
            Ui.Text(isMe ? $"{entry.Nick} (вы)" : entry.Nick, x + 40, y + 6, 20, isMe ? Theme.Me : Theme.Text);
            y += 36;
        }
        y += 14;

        // пинг и подсказка
        Ui.Text($"пинг {(pingMs < 0 ? "—" : $"{pingMs} мс")}", x, y, 18, Theme.Muted);
        y += 30;
        var hint = myRole == Role.Ghost
            ? "WASD — ходить. ЛКМ рядом с сундуком 3 с — проклясть. Красное — лучи фонарей, туда нельзя."
            : "WASD — ходить. Мышь — куда светить. ЛКМ — фонарь.";
        Ui.Paragraph(hint, x, y, width, 17, Theme.Muted);
    }

    // Время раунда вышло: затемнить карту и показать окошко. true — нажали «В комнату».
    public static bool DrawRoundOver(GameLayout layout)
    {
        Raylib.DrawRectangleRec(layout.Map, Theme.Overlay);
        var center = layout.MapOrigin + new Vector2(layout.MapWidth, layout.MapHeight) / 2;
        var box = new Rectangle(center.X - 220, center.Y - 100, 440, 200);
        Ui.Panel(box);
        Ui.TextCentered("Время вышло", center.X, box.Y + 32, 40);
        return Ui.Button(new Rectangle(box.X + 110, box.Y + 112, 220, 56), "В комнату", 24, primary: true);
    }
}
