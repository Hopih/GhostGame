namespace GhostGame.Shared;

public record InputState(sbyte MoveX, sbyte MoveY, float AimX, float AimY, byte Actions)
{
    public const byte ActionPrimary = 1;

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

public record Snapshot(uint Tick, ushort TimeLeft, List<SnapshotPlayer> Players, byte CurseMask, byte CatchProgress = 0,
    byte[]? CurseProgress = null)
{
    public const int ItemCount = 5;

    public bool IsCursed(int itemIndex) => (CurseMask & (1 << itemIndex)) != 0;

    public byte CurseProgressOf(int itemIndex) => CurseProgress is { } p && itemIndex < p.Length ? p[itemIndex] : (byte)0;
}
