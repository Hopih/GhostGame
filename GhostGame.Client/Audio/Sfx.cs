using Raylib_cs;

namespace GhostGame.Client.Audio;

// Все звуки клиента. Файлы — из паков Kenney (лицензия CC0, assets/sounds/kenney-audio-license.txt):
// Interface Sounds, RPG Audio, Music Jingles. Загружаются один раз после InitWindow.
// Нет файла или звуковой карты — играть просто нечего, игра от этого не падает.
public static class Sfx
{
    // имена — это имена файлов в assets/sounds без .ogg
    public const string Click = "click_002";
    public const string Error = "error_006";
    public const string Chat = "pluck_001";
    public const string Tick = "tick_002";
    public const string FlashOn = "metalClick";
    public const string FlashOff = "metalLatch";
    public const string Heartbeat = "dropLeather";
    public const string ChestSlam = "doorClose_3";
    public const string RoundStart = "creak2";
    public const string Spotted = "jingles_HIT03";
    public const string Win = "jingles_PIZZI07";
    public const string Lose = "jingles_HIT15";
    public static readonly string[] Footsteps = Enumerable.Range(0, 10).Select(i => $"footstep{i:00}").ToArray();
    public static readonly string[] Creaks = { "creak1", "creak2", "creak3" };
    public static readonly string[] Scratches = { "scratch_004", "scratch_005" };

    private static readonly Dictionary<string, Sound> Sounds = new();
    private static bool muted;

    public static bool Muted => muted;

    public static void Load()
    {
        Raylib.InitAudioDevice();
        var folder = Path.Combine(AppContext.BaseDirectory, "assets", "sounds");
        if (!Directory.Exists(folder))
        {
            return;
        }
        foreach (var file in Directory.GetFiles(folder, "*.ogg"))
        {
            var sound = Raylib.LoadSound(file);
            if (Raylib.IsSoundValid(sound))
            {
                Sounds[Path.GetFileNameWithoutExtension(file)] = sound;
            }
        }
    }

    // Сыграть звук с громкостью volume (0–1) и высотой pitch (1 — как записан; меньше — ниже и медленнее).
    // Если этот же звук ещё играет, он начнётся заново.
    public static void Play(string name, float volume = 1, float pitch = 1)
    {
        if (volume <= 0.01f || !Sounds.TryGetValue(name, out var sound))
        {
            return;
        }
        Raylib.SetSoundVolume(sound, Math.Clamp(volume, 0, 1));
        Raylib.SetSoundPitch(sound, pitch);
        Raylib.PlaySound(sound);
    }

    // Случайный звук из набора — чтобы шаги и скрипы не звучали одинаково
    public static void PlayAny(string[] names, float volume = 1, float pitch = 1)
    {
        Play(names[Random.Shared.Next(names.Length)], volume, pitch * (0.92f + 0.16f * Random.Shared.NextSingle()));
    }

    // Раз в кадр. Окно без фокуса молчит: несколько клиентов на одном компьютере не звучат хором.
    public static void Update()
    {
        Raylib.SetMasterVolume(muted || !Raylib.IsWindowFocused() ? 0 : 1);
    }

    public static void ToggleMute() => muted = !muted;

    public static void Unload()
    {
        foreach (var sound in Sounds.Values)
        {
            Raylib.UnloadSound(sound);
        }
        Raylib.CloseAudioDevice();
    }
}
