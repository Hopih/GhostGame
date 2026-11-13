namespace GhostGame.Shared;

public enum GameOverReason : byte
{
    GhostCaught = 1,
    Cursed = 2,
    HuntersLeft = 3,
    TimeUp = 4,
    GhostLeft = 5,
}
