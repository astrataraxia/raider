// 플랫폼 어댑터가 현재 라이브 목록을 수집하는 최소 계약을 정의한다.
using System.Collections.Immutable;
using Raider.Web.Live;

namespace Raider.Web.Collection;

public interface ILiveSource
{
    Platform Platform { get; }

    Task<ImmutableArray<LiveStream>> CollectAsync(CancellationToken cancellationToken);
}

public interface IProgressiveLiveSource : ILiveSource
{
    Task<ImmutableArray<LiveStream>> CollectAsync(
        Func<ImmutableArray<LiveStream>, ValueTask> publishPartial,
        CancellationToken cancellationToken);
}
