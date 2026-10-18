namespace GhostGame.Shared;

// Error.code (ТЗ 3.5)
public enum ErrorCode : byte
{
    BadNick = 1,
    RoomNotFound = 2,
    RoomFull = 3,
    GameInProgress = 4,
    UnexpectedPacket = 5,
    UnsupportedVersion = 6,
}