namespace GhostGame.Shared;

// Данные раунда, общие для сервера и клиента: через них пишутся и читаются Input и Snapshot.

// Что игрок нажал: Input без inputSeq — номер добавляется при отправке
public record InputState(sbyte MoveX, sbyte MoveY, float AimX, float AimY, byte Actions)
{
    public const byte ActionPrimary = 1; // бит 0: охотник — фонарь включён, призрак — проклинать предмет (удержание)

    public static readonly InputState Idle = new(0, 0, 0, 0, 0);

    public bool Primary => (Actions & ActionPrimary) != 0;
}

public record SnapshotPlayer(int PlayerId, float X, float Y, float AimX, float AimY, byte Flags , byte Charge)
{
    public const byte FlagFlashlight = 1;

    public const byte FlashlightOverheated = 2;
    
    public bool FlashlightOn => (Flags & FlagFlashlight) != 0;
    
    public bool Overheated => (Flags & FlashlightOverheated) != 0;
    
}

// Снимок раунда: tick, оставшееся время, игроки (призрак — только если видим), маска проклятых предметов,
// сколько призрак уже набрал до поимки (0–100 %) и прогресс проклятия каждого из 5 предметов (0–100 %)
public record Snapshot(uint Tick, ushort TimeLeft, List<SnapshotPlayer> Players, byte CurseMask, byte CatchProgress = 0,
    byte[]? CurseProgress = null)
{
    public const int ItemCount = 5;

    public bool IsCursed(int itemIndex) => (CurseMask & (1 << itemIndex)) != 0;

    // Прогресс проклятия предмета в %; если в снимке его нет — 0
    public byte CurseProgressOf(int itemIndex) => CurseProgress is { } p && itemIndex < p.Length ? p[itemIndex] : (byte)0;
}
