namespace GhostGame.Shared;

// Disconnect.reason (ТЗ 3.5)
public enum DisconnectReason : byte
{
    Timeout = 1,
    ServerShutdown = 2,
}
