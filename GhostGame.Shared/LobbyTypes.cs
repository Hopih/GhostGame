namespace GhostGame.Shared;

// Роль игрока в раунде (GameStart.role, GameOver.winner и роли в итогах)
public enum Role : byte
{
    Hunter = 0,
    Ghost = 1,
}

// Элемент RoomList: «табличка на двери» комнаты
public record RoomInfo(int RoomId, string Name, byte Players, byte MaxPlayers, bool InGame);

// Элемент RoomState: игрок в комнате
public record RoomPlayer(int PlayerId, string Nick, bool Ready);

// Элемент GameStart: где появляется игрок
public record PlayerSpawn(int PlayerId, float X, float Y);

// Элемент GameOver: кем был игрок в раунде
public record PlayerRole(int PlayerId, Role Role);
