using System.Numerics;

namespace GhostGame.Shared;

public static class Flashlight
{
    public const float Length = 5f;
    public const float AngleDegrees = 60f;
    private const float Step = 0.05f;

    private static readonly float MinCos = MathF.Cos(AngleDegrees / 2 * MathF.PI / 180);

    // Попадает ли target в луч фонаря из from в сторону aim (всё в клетках): не дальше 5 клеток,
    // не дальше 30° от прицела (сравниваем косинусы) и без стен и высокой мебели на пути.
    public static bool Covers(GameMap map, Vector2 from, Vector2 aim, Vector2 target)
    {
        var toTarget = target - from;
        var distance = toTarget.Length();
        if (distance > Length)
        {
            return false;
        }
        if (distance < 0.001f)
        {
            return true;
        }

        var direction = aim - from;
        if (direction.Length() < 0.001f)
        {
            return false;
        }

        var cos = Vector2.Dot(Vector2.Normalize(direction), toTarget / distance);
        if (cos < MinCos)
        {
            return false;
        }

        return NoWallsBetween(map, from, target, distance);
    }

    private static bool NoWallsBetween(GameMap map, Vector2 from, Vector2 target, float distance)
    {
        var steps = (int)(distance / Step);
        for (var i = 1; i <= steps; i++)
        {
            var point = Vector2.Lerp(from, target, i / (float)steps);
            if (map.BlocksLight((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y)))
            {
                return false;
            }
        }
        return true;
    }
}
