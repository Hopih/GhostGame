namespace GhostGame.Shared;

public enum ErrorCode : byte
{
    BadNick = 1,
    RoomNotFound = 2,
    RoomFull = 3,
    GameInProgress = 4,
    UnexpectedPacket = 5,
    UnsupportedVersion = 6,
}