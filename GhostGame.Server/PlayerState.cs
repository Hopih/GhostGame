using GhostGame.Shared;

namespace GhostGame.Server;

// Состояние одного игрока в раунде: кто он, где стоит и что нажал последним.
// Живёт только пока идёт раунд; данные подключения (ник, id, адрес) — в ClientSession.
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

    // где стоит, в клетках; меняется каждый тик
    public float X { get; set; }
    public float Y { get; set; }

    // последний принятый Input: что игрок держит нажатым сейчас; пока ничего не прислал — стоит
    public InputState Input { get; set; } = InputState.Idle;

    // номер последнего принятого Input: всё, что придёт с номером не больше, — опоздавшее, выбрасываем (ТЗ 4.3)
    public uint LastInputSeq { get; set; }

    // когда пришёл последний Input: дольше 500 мс без Input — игрок стоит на месте (ТЗ 4.3)
    public long LastInputAt { get; set; }

    // горит ли фонарь в этом тике: охотник держит ЛКМ (с учётом таймаута 500 мс); ставит игровой цикл
    public bool FlashlightOn { get; set; }

    // клеток в секунду (ТЗ 4.3)
    public float Speed => Role == Role.Ghost ? 4.5f : 4f;
}
