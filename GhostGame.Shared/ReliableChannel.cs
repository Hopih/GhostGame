using System.Net;

namespace GhostGame.Shared;

// Надёжный канал поверх UDP (ТЗ 3.6): нумерация, подтверждения, повторы, отсев дубликатов, порядок.
// Один экземпляр на собеседника. Состояние общее для цикла приёма и таймера повторов, поэтому всё под lock.
public sealed class ReliableChannel
{
    public const int ResendAfterMs = 200;
    public const int MaxBuffered = 64;
    public const int FailAfterMs = 10_000;

    private readonly object sync = new();
    private readonly Transport transport;
    private readonly EndPoint peer;

    private uint nextSendSeq = 1;
    private uint nextExpected = 1;
    private readonly Dictionary<uint, Pending> unacked = new();    //неподтвержденные пакеты
    private readonly Dictionary<uint, Packet> outOfOrder = new();  //пакеты пришедшие раньше времени
    private int totalResent;

    public ReliableChannel(Transport transport, EndPoint peer)
    {
        this.transport = transport;
        this.peer = peer;
    }

    public int TotalResent
    {
        get { lock (sync) { return totalResent; } }
    }

    public int PendingCount
    {
        get { lock (sync) { return unacked.Count; } }
    }

    public static bool IsReliable(PacketType type) => type is
        PacketType.Hello or PacketType.ListRooms or PacketType.CreateRoom or PacketType.JoinRoom
        or PacketType.LeaveRoom or PacketType.SetReady or PacketType.Chat
        or PacketType.Welcome or PacketType.RoomList or PacketType.RoomState or PacketType.ChatMessage
        or PacketType.GameStart or PacketType.GameEvent or PacketType.GameOver or PacketType.Error;

    // Отправка: следующий seq, в сеть и в таблицу неподтверждённых
    public Task SendAsync(PacketType type, byte[] payload)
    {
        if (!IsReliable(type))
        {
            throw new ArgumentException($"{type} is not a reliable packet", nameof(type));
        }

        byte[] datagram;
        lock (sync)
        {
            var seq = nextSendSeq++;
            datagram = PacketIO.Build(type, seq, payload);
            unacked[seq] = new Pending(datagram, Environment.TickCount64);
        }
        return transport.SendAsync(datagram, peer);
    }

    // Приём: возвращает пакеты, которые можно обрабатывать прямо сейчас, по порядку (может быть пусто)
    public async Task<IReadOnlyList<Packet>> ReceiveAsync(Packet packet)
    {
        if (packet.Type == PacketType.Ack)
        {
            var ackSeq = Packets.ReadAck(packet.Payload);
            lock (sync)
            {
                unacked.Remove(ackSeq);
            }
            return [];
        }

        if (!IsReliable(packet.Type))
        {
            return [packet];
        }

        if (packet.Seq == 0)
        {
            throw new MalformedPacketException("Reliable packet without seq");
        }

        var ready = new List<Packet>();
        bool sendAck;
        lock (sync)
        {
            if (packet.Seq < nextExpected)
            {
                // дубликат: уже обработан, но наш Ack мог потеряться — подтверждаем ещё раз
                sendAck = true;
            }
            else if (packet.Seq == nextExpected)
            {
                sendAck = true;
                ready.Add(packet);
                nextExpected++;
                while (true)
                {
                    if (!outOfOrder.ContainsKey(nextExpected)) // следующего номера в буфере нет?
                    {
                        break;                                 
                    }

                    var next = outOfOrder[nextExpected];        
                    outOfOrder.Remove(nextExpected);            
                    ready.Add(next);                            
                    nextExpected++;                             
                }
            }
            else if (packet.Seq - nextExpected < MaxBuffered)
            {
                // пришёл раньше предыдущего — ждёт в буфере
                sendAck = true;
                outOfOrder.TryAdd(packet.Seq, packet);
            }
            else
            {
                // слишком далеко вперёд: не подтверждаем, отправитель повторит позже
                sendAck = false;
            }
        }

        if (sendAck)
        {
            await transport.SendUnreliableAsync(PacketType.Ack, Packets.WriteAck(packet.Seq), peer);
        }
        return ready;
    }

    // Вызывается раз в 50 мс: повторяет пакеты без Ack дольше 200 мс.
    // Failed = какой-то пакет не подтверждён 10 с, собеседник считается отключившимся.
    public async Task<(int Resent, bool Failed)> ResendAsync()
    {
        var toSend = new List<byte[]>();
        var failed = false;
        lock (sync)
        {
            var now = Environment.TickCount64;
            foreach (var pending in unacked.Values)
            {
                if (now - pending.FirstSentAt > FailAfterMs)
                {
                    failed = true;
                }
                if (now - pending.LastSentAt >= ResendAfterMs)
                {
                    pending.LastSentAt = now;
                    toSend.Add(pending.Datagram);
                }
            }
            totalResent += toSend.Count;
        }

        foreach (var datagram in toSend)
        {
            await transport.SendAsync(datagram, peer);
        }
        return (toSend.Count, failed);
    }

    private sealed class Pending
    {
        public Pending(byte[] datagram, long sentAt)
        {
            Datagram = datagram;
            FirstSentAt = sentAt;
            LastSentAt = sentAt; //???
        }

        public byte[] Datagram { get; }
        public long FirstSentAt { get; }
        public long LastSentAt { get; set; }
    }
}
