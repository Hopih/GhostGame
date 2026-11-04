using System.Numerics;
using Raylib_cs;

namespace GhostGame.Client.Graphics;

// Темнота и свет — только картинка для атмосферы. Кого на самом деле видно, решает сервер (туман войны, этап 6).
// Как устроено: отдельная «плёнка» (RenderTexture) размером с окно в мир заливается почти чёрным,
// в ней «стираются» светлые места — вокруг себя, вокруг охотников, лучи фонарей, предметы —
// и плёнка кладётся поверх уже нарисованных карты и игроков.
public sealed class LightRenderer
{
    // Режим смешивания «стирать»: новый цвет плёнки = старый × (1 − непрозрачность того, что рисуем).
    // Рисуем непрозрачное — плёнка там становится прозрачной, полупрозрачное — стирается частично.
    private const int GlZero = 0;
    private const int GlOneMinusSrcAlpha = 0x0303;
    private const int GlFuncAdd = 0x8006;

    private RenderTexture2D film;

    // lamps — факелы и чаши на карте: вокруг них всегда светло. Плёнка размером с окно в мир, не со всю карту.
    public void Draw(GameLayout layout, IReadOnlyList<PlayerView> players, IReadOnlyList<Vector2> items,
        IReadOnlyList<Vector2> lamps, bool ghostView)
    {
        var view = layout.Viewport;
        Resize((int)view.Width, (int)view.Height);
        var cell = layout.Cell;
        var corner = new Vector2(view.X, view.Y);
        Vector2 OnFilm(Vector2 cells) => layout.ToScreen(cells) - corner; // клетки карты → пиксели внутри плёнки

        Raylib.BeginTextureMode(film);
        Raylib.ClearBackground(new Color(8, 5, 12, ghostView ? 80 : 190)); // призрак видит всю карту (ТЗ 5.1) — ему темнота слабая
        Rlgl.SetBlendFactors(GlZero, GlOneMinusSrcAlpha, GlFuncAdd);
        Raylib.BeginBlendMode(BlendMode.Custom);

        foreach (var lamp in lamps)
        {
            Hole(OnFilm(lamp), cell * 2.4f, 170);
        }
        foreach (var item in items)
        {
            Hole(OnFilm(item), cell * 1.1f, 160);
        }
        foreach (var p in players)
        {
            var at = OnFilm(p.Position);
            if (p.IsMe)
            {
                Hole(at, cell * 3.2f, 255);
            }
            else if (!p.IsGhost)
            {
                Hole(at, cell * 1.6f, 200); // у каждого охотника фонарь светит и вокруг него
            }
            if (PlayerRenderer.BeamShown(p)) // садящийся фонарь мигает и в темноте
            {
                Beam(at, PlayerRenderer.BeamAngle(p), cell);
            }
        }

        Raylib.EndBlendMode();
        Raylib.EndTextureMode();

        // у RenderTexture ось Y перевёрнута (так устроен OpenGL) — поэтому высота источника со знаком минус
        var source = new Rectangle(0, 0, film.Texture.Width, -film.Texture.Height);
        Raylib.DrawTexturePro(film.Texture, source, view, Vector2.Zero, 0, Color.White);
    }

    // Мягкое пятно света: в центре плёнка стирается сильнее всего, к краю всё слабее
    private static void Hole(Vector2 center, float radius, byte strength)
    {
        Raylib.DrawCircleGradient((int)center.X, (int)center.Y, radius,
            new Color(255, 255, 255, (int)strength), new Color(255, 255, 255, 0));
    }

    // Луч фонаря: пять конусов всё короче — у фонаря светлее, к концу луча темнее
    private static void Beam(Vector2 from, float angle, int cell)
    {
        var half = PlayerRenderer.FlashlightAngle / 2;
        for (var i = 0; i < 5; i++)
        {
            var length = PlayerRenderer.FlashlightLength * cell * (1 - i * 0.16f);
            Raylib.DrawCircleSector(from, length, angle - half, angle + half, 24, new Color(255, 255, 255, 90));
        }
    }

    // Плёнка должна совпадать с картой по размеру; окно растянули — пересоздаём
    private void Resize(int width, int height)
    {
        if (film.Texture.Width == width && film.Texture.Height == height)
        {
            return;
        }
        if (film.Id != 0)
        {
            Raylib.UnloadRenderTexture(film);
        }
        film = Raylib.LoadRenderTexture(width, height);
    }
}
