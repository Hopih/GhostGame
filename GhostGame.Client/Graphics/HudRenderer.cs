using System.Numerics;
using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Панель справа от карты: роль, таймер, поимка, проклятые предметы, мини-карта, игроки, пинг, подсказка
public static class HudRenderer
{
    public static void Draw(GameLayout layout, GameMap map, IReadOnlyList<PlayerView> players, Role myRole, int myId,
        ushort timeLeft, byte curseMask, byte catchProgress, IReadOnlyList<RosterEntry> roster, long pingMs)
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

        // поимка: сколько призрак уже простоял в лучах. Призраку — тревога, охотникам — сколько осталось дожать.
        Ui.Text(myRole == Role.Ghost ? $"ВАС ЛОВЯТ  {catchProgress}%" : $"ПОИМКА  {catchProgress}%", x, y, 16,
            myRole == Role.Ghost && catchProgress > 0 ? Theme.Error : Theme.Muted);
        Ui.ProgressBar(new Rectangle(x, y + 22, width, 14), catchProgress / 100f,
            myRole == Role.Ghost ? Theme.Error : Theme.Accent);
        y += 54;

        // проклятые предметы: сундук — цел, мимик — проклят; призраку для победы нужно 3 (ТЗ 6)
        Ui.Text($"ПРОКЛЯТО  {BitOperations.PopCount(curseMask)} / 3", x, y, 16, Theme.Muted);
        var icon = MathF.Min(40, (width - 4 * 6) / 5);
        for (var i = 0; i < 5; i++)
        {
            var cursed = (curseMask & (1 << i)) != 0;
            Assets.DrawSprite(cursed ? Sprites.Mimic : Sprites.Chest, new Rectangle(x + i * (icon + 6), y + 22, icon, icon));
        }
        y += 22 + icon + 20;

        // мини-карта всего особняка
        y += DrawMinimap(layout, map, players, curseMask, x, y, width) + 16;

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

    // Мини-карта: весь особняк по клетке на несколько пикселей, сундуки, видимые игроки и рамка «что сейчас в окне».
    // Игроки — те же, что и на экране, поэтому охотник видит на ней призрака, только пока тот в луче (туман войны).
    // Возвращает высоту, которую заняла.
    private static float DrawMinimap(GameLayout layout, GameMap map, IReadOnlyList<PlayerView> players, byte curseMask,
        float x, float y, float width)
    {
        Ui.Text("КАРТА", x, y, 16, Theme.Muted);
        var top = y + 22;
        var dot = MathF.Max(1, MathF.Floor(width / GameMap.Width)); // пикселей на клетку — целое, чтобы не было швов
        var left = x + (width - dot * GameMap.Width) / 2;
        Vector2 At(Vector2 cells) => new(left + cells.X * dot, top + cells.Y * dot);

        for (var cy = 0; cy < GameMap.Height; cy++)
        {
            for (var cx = 0; cx < GameMap.Width; cx++)
            {
                var color = map.IsWall(cx, cy) ? Theme.MiniWall
                    : map.IsSolid(cx, cy) ? Theme.MiniFurniture
                    : map[cx, cy] switch
                    {
                        ',' => Theme.MiniStone,
                        '=' => Theme.MiniWood,
                        '~' => Theme.MiniCarpet,
                        'D' => Theme.Wood,
                        _ => Theme.MiniSand,
                    };
                Raylib.DrawRectangleRec(new Rectangle(left + cx * dot, top + cy * dot, dot, dot), color);
            }
        }

        for (var i = 0; i < map.Items.Count; i++)
        {
            var cursed = (curseMask & (1 << i)) != 0;
            Raylib.DrawCircleV(At(map.Items[i]), dot * 1.3f, cursed ? Theme.CurseGlow : Theme.Accent);
        }
        foreach (var p in players)
        {
            var color = p.IsMe ? Theme.Me : p.IsGhost ? Theme.Stone : Theme.MiniHunter;
            Raylib.DrawCircleV(At(p.Position), dot * (p.IsMe ? 1.8f : 1.4f), color);
        }

        // какая часть карты сейчас в окне
        var view = layout.Viewport;
        var a = layout.ToCells(new Vector2(view.X, view.Y));
        var b = layout.ToCells(new Vector2(view.X + view.Width, view.Y + view.Height));
        var frame = new Rectangle(At(a).X, At(a).Y, (b.X - a.X) * dot, (b.Y - a.Y) * dot);
        Raylib.BeginScissorMode((int)left, (int)top, (int)(dot * GameMap.Width), (int)(dot * GameMap.Height));
        Raylib.DrawRectangleLinesEx(frame, 1, Raylib.Fade(Theme.Text, 0.6f));
        Raylib.EndScissorMode();

        return 22 + dot * GameMap.Height;
    }

    // Время раунда вышло: затемнить окно в мир и показать окошко. true — нажали «В комнату».
    public static bool DrawRoundOver(GameLayout layout)
    {
        var view = layout.Viewport;
        Raylib.DrawRectangleRec(view, Theme.Overlay);
        var center = new Vector2(view.X + view.Width / 2, view.Y + view.Height / 2);
        var box = new Rectangle(center.X - 220, center.Y - 100, 440, 200);
        Ui.Panel(box);
        Ui.TextCentered("Время вышло", center.X, box.Y + 32, 40);
        return Ui.Button(new Rectangle(box.X + 110, box.Y + 112, 220, 56), "В комнату", 24, primary: true);
    }
}
