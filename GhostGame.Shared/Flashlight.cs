using System.Numerics;

namespace GhostGame.Shared;

// Фонарь охотника (ТЗ 6.3): конус длиной 5 клеток и шириной 60° в сторону прицела, сквозь стены не светит.
// По нему сервер решает, видим ли призрак (туман войны, ТЗ 4.4), идёт ли поимка и сбрасывается ли проклятие.
// Включён ли фонарь (охотник держит ЛКМ), проверяет вызывающий — здесь только геометрия.
public static class Flashlight
{
    public const float Length = 5f;         // клеток
    public const float AngleDegrees = 60f;  // полная ширина конуса
    private const float Step = 0.05f;       // шаг, с которым идём вдоль луча и ищем стены, в клетках

    // Точка в конусе, если угол между «куда светим» и «где точка» не больше половины ширины, то есть 30°.
    // Сравнивать удобнее косинусы: чем меньше угол, тем больше косинус. cos 30° ≈ 0,866.
    private static readonly float MinCos = MathF.Cos(AngleDegrees / 2 * MathF.PI / 180);

    // Попадает ли точка target в луч фонаря, который стоит в from и светит в сторону aim. Всё в клетках.
    public static bool Covers(GameMap map, Vector2 from, Vector2 aim, Vector2 target)
    {
        var toTarget = target - from;
        var distance = toTarget.Length();
        if (distance > Length)
        {
            return false; // дальше конца луча
        }
        if (distance < 0.001f)
        {
            return true; // стоят в одной точке
        }

        var direction = aim - from;
        if (direction.Length() < 0.001f)
        {
            return false; // мышь ровно на охотнике — непонятно, куда светить
        }

        // Косинус угла между двумя направлениями — скалярное произведение векторов длины 1
        var cos = Vector2.Dot(Vector2.Normalize(direction), toTarget / distance);
        if (cos < MinCos)
        {
            return false; // точка сбоку от конуса или сзади
        }

        return NoWallsBetween(map, from, target, distance);
    }

    // Идём от фонаря к точке шагами по 0,05 клетки: попалась по пути стена — луч до точки не доходит.
    // Дверь (D) — не стена, через дверной проём светит.
    private static bool NoWallsBetween(GameMap map, Vector2 from, Vector2 target, float distance)
    {
        var steps = (int)(distance / Step);
        for (var i = 1; i <= steps; i++)
        {
            var point = Vector2.Lerp(from, target, i / (float)steps); // i-я точка на отрезке от from до target
            if (map.IsWall((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y)))
            {
                return false;
            }
        }
        return true;
    }
}
