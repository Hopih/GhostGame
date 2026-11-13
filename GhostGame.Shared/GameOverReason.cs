namespace GhostGame.Shared;

public enum GameOverReason : byte
{
    GhostCaught = 1,   // призрак пойман — победа охотников
    Cursed = 2,        // проклято 3 предмета — победа призрака
    HuntersLeft = 3,   // все охотники отключились — победа призрака
    TimeUp = 4,        // вышло время — победа призрака
    GhostLeft = 5,     // призрак отключился — победа охотников
}
