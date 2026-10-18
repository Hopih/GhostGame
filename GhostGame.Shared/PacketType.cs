namespace GhostGame.Shared;

public enum PacketType : byte
{
    // Клиент → сервер (ТЗ 3.4)
    Hello = 0x01,
    Ping = 0x02,
    ClientDisconnect = 0x03,
    ListRooms = 0x10,
    CreateRoom = 0x11,
    JoinRoom = 0x12,
    LeaveRoom = 0x13,
    SetReady = 0x14,
    Chat = 0x15,
    Input = 0x20,

    // В обе стороны
    Ack = 0x7F,

    // Сервер → клиент (ТЗ 3.5)
    Welcome = 0x81,
    Disconnect = 0x80,
    Pong = 0x82,
    ServerDisconnect = 0x83,
    RoomList = 0x90,
    RoomState = 0x91,
    ChatMessage = 0x95,
    GameStart = 0xA0,
    Snapshot = 0xA1,
    GameEvent = 0xA2,
    GameOver = 0xA3,
    Error = 0xF0,
}
