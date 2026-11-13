using System.Numerics;

namespace GhostGame.Client.Graphics;

public readonly record struct PlayerView(
    int PlayerId,
    string Nick,
    Vector2 Position,
    Vector2 Aim,
    bool FlashlightOn,
    bool IsGhost,
    bool IsMe,
    bool Moving,
    byte Charge,
    bool Overheated);

public readonly record struct RosterEntry(int PlayerId, string Nick, bool IsGhost);
