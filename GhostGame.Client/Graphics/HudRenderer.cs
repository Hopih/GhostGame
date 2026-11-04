using System.Numerics;
using GhostGame.Client.Audio;
using GhostGame.Shared;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Панель справа от карты: роль, таймер, поимка, проклятые предметы, мини-карта, игроки, пинг, подсказка
public static class HudRenderer
{
    // snapshot — время, поимка, маска и прогресс проклятий; cursing[i] — сундук i проклинают прямо сейчас
    public static void Draw(GameLayout layout, GameMap map, IReadOnlyList<PlayerView> players, Role myRole, int myId,
        Snapshot snapshot, IReadOnlyList<bool> cursing, IReadOnlyList<RosterEntry> roster, long pingMs)
    {
        var timeLeft = snapshot.TimeLeft;
        var curseMask = snapshot.CurseMask;
        var catchProgress = snapshot.CatchProgress;
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

        // охотнику — заряд своего фонаря; сел — красная полоска, пока не остынет до конца
        var me = players.FirstOrDefault(p => p.IsMe);
        if (myRole == Role.Hunter && me.IsMe)
        {
            var low = !me.Overheated && me.Charge < 20;
            Ui.Text(me.Overheated ? "ФОНАРЬ ПЕРЕГРЕЛСЯ — ОСТЫВАЕТ" : $"ФОНАРЬ  {me.Charge}%", x, y, 16,
                me.Overheated || low ? Theme.Error : Theme.Muted);
            Ui.ProgressBar(new Rectangle(x, y + 22, width, 14), me.Charge / 100f,
                me.Overheated ? Theme.Error : low ? Theme.Accent : Theme.Flame);
            y += 54;
        }

        // проклятые предметы: сундук — цел, мимик — проклят, под сундуком — сколько уже набрано;
        // тот, что проклинают прямо сейчас, мигает. Призраку для победы нужно 3 (ТЗ 6).
        var anyCursing = cursing.Any(c => c);
        Ui.Text(anyCursing ? $"ПРОКЛИНАЮТ!  {BitOperations.PopCount(curseMask)} / 3" : $"ПРОКЛЯТО  {BitOperations.PopCount(curseMask)} / 3",
            x, y, 16, anyCursing ? Theme.CurseGlow with { A = 255 } : Theme.Muted);
        var icon = MathF.Min(40, (width - 4 * 6) / 5);
        var blink = Raylib.GetTime() % 0.5 < 0.25;
        for (var i = 0; i < Snapshot.ItemCount; i++)
        {
            var cursed = snapshot.IsCursed(i);
            var slot = new Rectangle(x + i * (icon + 6), y + 22, icon, icon);
            Assets.DrawSprite(cursed ? Sprites.Mimic : Sprites.Chest, slot,
                cursing[i] && blink ? Theme.CurseGlow with { A = 255 } : Color.White);
            var progress = snapshot.CurseProgressOf(i);
            if (!cursed && progress > 0)
            {
                Ui.ProgressBar(new Rectangle(slot.X, slot.Y + icon + 3, icon, 6), progress / 100f, Theme.CurseGlow with { A = 255 });
            }
        }
        y += 22 + icon + 26;

        // мини-карта всего особняка
        y += DrawMinimap(layout, map, players, curseMask, cursing, x, y, width) + 16;

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
        Ui.Text(Sfx.Muted ? "звук выключен" : "звук включён", x + width / 2, y, 18, Sfx.Muted ? Theme.Error : Theme.Muted);
        y += 30;
        var hint = myRole == Role.Ghost
            ? "WASD — ходить. ЛКМ рядом с сундуком 3 с — проклясть. Красное — лучи фонарей, туда нельзя. M — звук."
            : "WASD — ходить. Мышь — куда светить. ЛКМ — фонарь. M — звук.";
        Ui.Paragraph(hint, x, y, width, 17, Theme.Muted);
    }

    // Мини-карта: весь особняк по клетке на несколько пикселей, сундуки, видимые игроки и рамка «что сейчас в окне».
    // Игроки — те же, что и на экране, поэтому охотник видит на ней призрака, только пока тот в луче (туман войны).
    // Возвращает высоту, которую заняла.
    private static float DrawMinimap(GameLayout layout, GameMap map, IReadOnlyList<PlayerView> players, byte curseMask,
        IReadOnlyList<bool> cursing, float x, float y, float width)
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
            if (cursing[i])
            {
                // сундук проклинают — на мини-карте он пульсирует: охотники видят, куда бежать
                var ring = dot * (2 + 2 * (float)(Raylib.GetTime() * 3 % 1));
                Raylib.DrawCircleLinesV(At(map.Items[i]), ring, Theme.CurseGlow with { A = 255 });
            }
            Raylib.DrawCircleV(At(map.Items[i]), dot * 1.3f, cursed || cursing[i] ? Theme.CurseGlow with { A = 255 } : Theme.Accent);
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
