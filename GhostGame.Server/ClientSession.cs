namespace GhostGame.Server;

public class ClientSession
{
    private static int playerId = 1;
    
    public string Nick { get; set; }
    
    public int PlayerId { get; set; }
    
    public int RoomId { get; set; } = -1;
    
    public bool IsReady { get; set; } = false;
    
    public long LastPing { get; set; } = 0;
    
    public ClientSession(string nick)
    {
        Nick = nick;
        LastPing = Environment.TickCount64;
        PlayerId = playerId;
        playerId++;
    }
}