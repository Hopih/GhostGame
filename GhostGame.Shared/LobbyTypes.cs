namespace GhostGame.Shared;

// Роль игрока в раунде (GameStart.role, GameOver.winner и роли в итогах, ТЗ 3.5)
public enum Role : byte
{
    Hunter = 0,
    Ghost = 1,
}

// Элемент RoomList: «табличка на двери» комнаты (ТЗ 3.5)
public record RoomInfo(int RoomId, string Name, byte Players, byte MaxPlayers, bool InGame);

// Элемент RoomState: игрок в комнате (ТЗ 3.5)
public record RoomPlayer(int PlayerId, string Nick, bool Ready);

// Элемент GameStart: где появляется игрок (ТЗ 3.5)
public record PlayerSpawn(int PlayerId, float X, float Y);
