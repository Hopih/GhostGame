using GhostGame.Shared;

namespace GhostGame.Server;

public class PlayerState
{
    public PlayerState(ClientSession session, Role role, float x, float y)
    {
        Session = session;
        Role = role;
        X = x;
        Y = y;
        LastInputAt = Environment.TickCount64;
    }

    public ClientSession Session { get; }
    public Role Role { get; }

    public float Battery = 0;

    public float X { get; set; }
    public float Y { get; set; }

    public InputState Input { get; set; } = InputState.Idle;

    public uint LastInputSeq { get; set; }

    public long LastInputAt { get; set; }

    public bool PrimaryHeld { get; set; }

    public bool FlashlightOn { get; set; }

    public bool Muted { get; set; }

    public float Speed => Role == Role.Ghost ? 4.5f : 4f;

}
