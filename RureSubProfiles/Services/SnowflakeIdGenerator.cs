namespace RureSubProfiles.Services;

public class SnowflakeIdGenerator : ISnowflakeIdGenerator
{
    private readonly long customEpoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private const int WorkerIdBits = 10;
    private const int SequenceBits = 12;

    private const long MaxWorkerId = -1L ^ (-1L << WorkerIdBits);
    private const long MaxSequence = -1L ^ (-1L << SequenceBits);

    private const int WorkerIdShift = SequenceBits;
    private const int TimestampShift = WorkerIdBits + SequenceBits;

    private readonly long workerId;
    private readonly Lock @lock = new();

    private long lastTimestamp;
    private long sequence;

    public SnowflakeIdGenerator(long workerId)
    {
        if (workerId < 0 || workerId > MaxWorkerId)
        {
            throw new ArgumentOutOfRangeException(nameof(workerId), $"Worker ID должен быть в диапазоне от 0 до {MaxWorkerId}");
        }
        this.workerId = workerId;
    }

    public long NextId()
    {
        lock (@lock)
        {
            long timestamp = GetTimestamp();

            if (timestamp < lastTimestamp)
            {
                throw new InvalidOperationException($"Clock moved backwards! Rejecting requests for {lastTimestamp - timestamp} ms");
            }

            if (timestamp == lastTimestamp)
            {
                sequence = (sequence + 1) & MaxSequence;
                if (sequence == 0)
                {
                    timestamp = WaitNextMills(lastTimestamp);
                }
            }
            else
            {
                sequence = 0L;
            }

            long result = ((timestamp - customEpoch) << TimestampShift) |
                (workerId << WorkerIdShift) |
                sequence;

            lastTimestamp = timestamp;

            return result;
        }
    }

    public long GetTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public long WaitNextMills(long lastTimestamp)
    {
        long timestamp = GetTimestamp();
        while (timestamp <= lastTimestamp)
        {
            timestamp = GetTimestamp();
        }
        return timestamp;
    }

    public void ShowId(long id)
    {
        int size = 64;

        for (int i = size - 1; i >= 0; i--)
        {
            var lastColor = Console.ForegroundColor;
            if (i == 21 || i == 11 || i == 62)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write('|');
            }
            long bit = (id >> i) & 1;
            if (bit == 1)
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            Console.Write(bit);
            Console.ForegroundColor = lastColor;
        }
        Console.WriteLine();
    }
}
