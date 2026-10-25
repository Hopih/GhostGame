using GhostGame.Shared;
using System.Net;
namespace GhostGame.Server;

// Сессия создаётся на первый Hello с нового адреса, игроком становится после принятого Hello (Join)
public class ClientSession
{
    private static int nextPlayerId = 1;

    public ClientSession(ReliableChannel channel , IPEndPoint endPoint)
    {
        Channel = channel;
        EndPoint = endPoint;
        LastPing = Environment.TickCount64;
    }

    public ReliableChannel Channel { get; }

    public string? Nick { get; private set; }

    public int PlayerId { get; private set; }

    public bool IsJoined => Nick != null;
    
    public IPEndPoint EndPoint { get; }

    public int RoomId { get; set; } = -1;

    public bool IsReady { get; set; } = false;

    public long LastPing { get; set; }

    public void Join(string nick)
    {
        Nick = nick;
        PlayerId = nextPlayerId++;
    }
}
