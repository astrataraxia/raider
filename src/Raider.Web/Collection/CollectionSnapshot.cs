// 현재 라이브 읽기 모델과 플랫폼별 수집 상태를 한 번에 제공한다.
using System.Collections.Frozen;
using System.Collections.Immutable;
using Raider.Web.Live;

namespace Raider.Web.Collection;

public sealed record PlatformCollectionState(
    Platform Platform,
    ImmutableArray<LiveStream> Streams,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastAttemptAt,
    PlatformError? Error,
    bool IsPartial,
    TimeSpan? LastDuration)
{
    public bool AttemptCompleted => LastAttemptAt is not null;
}

public sealed record CollectionSnapshot(
    LiveSnapshot Live,
    FrozenDictionary<Platform, PlatformCollectionState> Platforms,
    DateTimeOffset ObservedAt,
    long Version)
{
    public bool IsReady => Platforms.Count > 0 && Platforms.Values.All(state => state.AttemptCompleted);
}
