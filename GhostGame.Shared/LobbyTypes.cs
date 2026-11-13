namespace GhostGame.Shared;

public enum Role : byte
{
    Hunter = 0,
    Ghost = 1,
}

public record RoomInfo(int RoomId, string Name, byte Players, byte MaxPlayers, bool InGame);

public record RoomPlayer(int PlayerId, string Nick, bool Ready);

public record PlayerSpawn(int PlayerId, float X, float Y);

public record PlayerRole(int PlayerId, Role Role);
