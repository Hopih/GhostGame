namespace GhostGame.Shared;

// Данные раунда, общие для сервера и клиента. Пакеты Input и Snapshot (ТЗ 3.4, 3.5) пишутся и читаются через эти типы.

// Что игрок нажал: Input без inputSeq — номер добавляется при отправке
public record InputState(sbyte MoveX, sbyte MoveY, float AimX, float AimY, byte Actions)
{
    public const byte ActionPrimary = 1; // бит 0: охотник — фонарь включён, призрак — проклинать предмет (удержание)

    public static readonly InputState Idle = new(0, 0, 0, 0, 0);

    public bool Primary => (Actions & ActionPrimary) != 0;
}

// Элемент списка игроков в Snapshot
public record SnapshotPlayer(int PlayerId, float X, float Y, float AimX, float AimY, byte Flags)
{
    public const byte FlagFlashlight = 1; // бит 0 flags: фонарь включён

    public bool FlashlightOn => (Flags & FlagFlashlight) != 0;
}

// Снимок раунда: tick, оставшееся время, игроки (призрак — только если видим), маска проклятых предметов
public record Snapshot(uint Tick, ushort TimeLeft, List<SnapshotPlayer> Players, byte CurseMask)
{
    public bool IsCursed(int itemIndex) => (CurseMask & (1 << itemIndex)) != 0;
}
