namespace Raider.Web.Collection;

public sealed class CollectionRegistry
{
    private readonly List<PlatformCollectorWorker> workers = [];
    private readonly object gate = new();

    public void Register(PlatformCollectorWorker worker)
    {
        ArgumentNullException.ThrowIfNull(worker);
        lock (gate)
        {
            workers.Add(worker);
        }
    }

    public void TriggerCollectAll()
    {
        PlatformCollectorWorker[] snapshot;
        lock (gate)
        {
            snapshot = [.. workers];
        }

        foreach (var worker in snapshot)
        {
            _ = worker.CollectOnceAsync(CancellationToken.None);
        }
    }

    public bool IsAnyCollecting
    {
        get
        {
            lock (gate)
            {
                return workers.Any(w => w.IsCollecting);
            }
        }
    }
}
