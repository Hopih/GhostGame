using System.Numerics;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Игроки: тень, фигурка, своё кольцо, ники и лучи фонарей
public static class PlayerRenderer
{
    public const float FlashlightLength = 5f;  // ТЗ 6.3: конус длиной 5 клеток
    public const float FlashlightAngle = 60f;  // и шириной 60°

    // Фигурки рисуются до темноты — в тёмном углу игрок тоже темнеет.
    // Охотник — свой спрайт и подпрыгивает на ходу, призрак — полупрозрачный и парит.
    public static void DrawBodies(IReadOnlyList<PlayerView> players, GameLayout layout, double time)
    {
        var cell = layout.Cell;
        foreach (var p in players.OrderBy(p => p.Position.Y)) // кто ниже на экране, рисуется позже и оказывается впереди
        {
            var feet = layout.ToScreen(p.Position) + new Vector2(0, cell * 0.3f);
            Raylib.DrawEllipse((int)feet.X, (int)feet.Y, cell * 0.32f, cell * 0.12f, Theme.Shadow);
            if (p.IsMe)
            {
                // себя видно по жёлтому кольцу под ногами
                Raylib.DrawEllipseLines((int)feet.X, (int)feet.Y, cell * 0.42f, cell * 0.17f, Theme.Me);
                Raylib.DrawEllipseLines((int)feet.X, (int)feet.Y, cell * 0.42f + 1, cell * 0.17f + 1, Theme.Me);
            }

            var lift = p.IsGhost
                ? cell * 0.15f + MathF.Sin((float)time * 3 + p.PlayerId) * cell * 0.06f
                : p.Moving ? MathF.Abs(MathF.Sin((float)time * 14)) * cell * 0.08f : 0;
            var dest = new Rectangle(feet.X - cell / 2f, feet.Y - cell * 0.95f - lift, cell, cell);
            var flip = p.Aim.X < p.Position.X; // смотрит туда, куда целится
            if (p.IsGhost)
            {
                Assets.DrawSprite(Sprites.Ghost, dest, Theme.GhostTint, flip);
            }
            else
            {
                Assets.DrawSprite(Sprites.Hunter(p.PlayerId), dest, flip);
            }
        }
    }

    // Лучи поверх темноты: у охотника — тёплый свет, у призрака — красная опасная зона.
    // Это только картинка: кого поймал луч, решает сервер (этап 6).
    public static void DrawBeams(IReadOnlyList<PlayerView> players, GameLayout layout, bool ghostView)
    {
        var color = ghostView ? Theme.DangerBeam : Theme.Beam;
        var map = layout.Map;
        Raylib.BeginScissorMode((int)map.X, (int)map.Y, (int)map.Width, (int)map.Height); // луч у края не вылезает за карту
        foreach (var p in players.Where(p => p.FlashlightOn))
        {
            var angle = BeamAngle(p);
            Raylib.DrawCircleSector(layout.ToScreen(p.Position), FlashlightLength * layout.Cell,
                angle - FlashlightAngle / 2, angle + FlashlightAngle / 2, 24, color);
        }
        Raylib.EndScissorMode();
    }

    // Направление луча в градусах: 0° — вправо, дальше по часовой (ось Y на экране вниз)
    public static float BeamAngle(PlayerView p)
    {
        return MathF.Atan2(p.Aim.Y - p.Position.Y, p.Aim.X - p.Position.X) * 180f / MathF.PI;
    }

    // Ники рисуются поверх темноты, чтобы читались всегда
    public static void DrawNames(IReadOnlyList<PlayerView> players, GameLayout layout)
    {
        var size = MathF.Max(14, layout.Cell * 0.36f);
        foreach (var p in players)
        {
            var center = layout.ToScreen(p.Position);
            var width = Ui.MeasureText(p.Nick, size);
            var top = center.Y - layout.Cell * (p.IsGhost ? 0.95f : 0.75f) - size - 4;
            var plate = new Rectangle(center.X - width / 2 - 6, top, width + 12, size + 4);
            Raylib.DrawRectangleRounded(plate, 0.5f, 6, Theme.Plate);
            Ui.Text(p.Nick, plate.X + 6, plate.Y + 2, size, p.IsMe ? Theme.Me : Theme.Text);
        }
    }
}
