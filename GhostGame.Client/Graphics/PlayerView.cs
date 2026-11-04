using System.Numerics;

namespace GhostGame.Client.Graphics;

// Один игрок в этом кадре — всё, что нужно, чтобы его нарисовать. Собирает GameScreen, рисуют рендереры.
// Position уже сглажена между двумя последними Snapshot; координаты в клетках карты.
// Charge — сколько осталось заряда фонаря, %; Overheated — фонарь сел и остывает (оба — из Snapshot).
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

// Строка списка игроков на панели: все участники раунда, даже те, кого сейчас не видно
public readonly record struct RosterEntry(int PlayerId, string Nick, bool IsGhost);
