using System.Collections.Concurrent;

namespace Raider.Web.Collection;

/// <summary>
/// 플랫폼별 백그라운드 수집기(PlatformCollectorWorker)들을 등록하고 일괄적으로 즉시 수집을 유도하거나 상태를 파악하는 레지스트리입니다.
/// </summary>
public sealed class CollectionRegistry
{
    private readonly ConcurrentBag<PlatformCollectorWorker> workers = new();

    /// <summary>
    /// 수집기를 레지스트리에 등록합니다.
    /// </summary>
    public void Register(PlatformCollectorWorker worker)
    {
        ArgumentNullException.ThrowIfNull(worker);
        workers.Add(worker);
    }

    /// <summary>
    /// 등록된 모든 수집기의 수집 루틴을 백그라운드에서 즉시 실행하도록 트리거합니다.
    /// </summary>
    public void TriggerCollectAll()
    {
        foreach (var worker in workers)
        {
            _ = worker.CollectOnceAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// 현재 수집 중인 백그라운드 서비스가 하나라도 존재하는지 여부를 판단합니다.
    /// </summary>
    public bool IsAnyCollecting => workers.Any(w => w.IsCollecting);
}
