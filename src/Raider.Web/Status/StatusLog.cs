// 수집과 채팅의 최근 경고를 메모리에 짧은 줄로만 남긴다.
namespace Raider.Web.Status;

public sealed class StatusLog
{
    public const int Capacity = 30;
    private readonly Queue<StatusEvent> events = new();
    private readonly object gate = new();

    public void Add(DateTimeOffset at, string platform, string operation, string errorKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorKind);
        lock (gate)
        {
            events.Enqueue(new StatusEvent(at, platform, operation, errorKind));
            while (events.Count > Capacity)
            {
                events.Dequeue();
            }
        }
    }

    public IReadOnlyList<StatusEvent> Recent()
    {
        lock (gate)
        {
            var copy = events.ToArray();
            Array.Reverse(copy);
            return copy;
        }
    }
}

public readonly record struct StatusEvent(DateTimeOffset At, string Platform, string Operation, string ErrorKind);
