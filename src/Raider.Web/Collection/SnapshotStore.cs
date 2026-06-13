// 플랫폼 결과를 병합해 완성된 최신 스냅샷 참조만 원자 교체한다.
using System.Collections.Frozen;
using System.Collections.Immutable;
using Raider.Web.Live;

namespace Raider.Web.Collection;

public sealed class SnapshotStore
{
    private readonly object updateLock = new();
    private CollectionSnapshot current;

    public SnapshotStore(IEnumerable<Platform> platforms)
    {
        var states = platforms
            .Distinct()
            .ToFrozenDictionary(
                platform => platform,
                platform => new PlatformCollectionState(platform, [], null, null, null));
        current = Build(states, DateTimeOffset.MinValue);
    }

    public CollectionSnapshot Current => Volatile.Read(ref current);

    public bool ApplySuccess(Platform platform, ImmutableArray<LiveStream> streams, DateTimeOffset completedAt)
    {
        lock (updateLock)
        {
            var snapshot = Current;
            if (!CanApply(snapshot, platform, completedAt))
            {
                return false;
            }

            var states = snapshot.Platforms.ToDictionary();
            states[platform] = new PlatformCollectionState(platform, streams, completedAt, completedAt, null);
            Interlocked.Exchange(ref current, Build(states.ToFrozenDictionary(), completedAt));
            return true;
        }
    }

    public bool ApplyFailure(Platform platform, PlatformError error, DateTimeOffset completedAt)
    {
        lock (updateLock)
        {
            var snapshot = Current;
            if (!CanApply(snapshot, platform, completedAt))
            {
                return false;
            }

            var previous = snapshot.Platforms[platform];
            var states = snapshot.Platforms.ToDictionary();
            states[platform] = previous with
            {
                LastAttemptAt = completedAt,
                Error = error,
            };
            Interlocked.Exchange(ref current, Build(states.ToFrozenDictionary(), completedAt));
            return true;
        }
    }

    private static bool CanApply(CollectionSnapshot snapshot, Platform platform, DateTimeOffset completedAt)
    {
        return snapshot.Platforms.TryGetValue(platform, out var previous)
            && (previous.LastAttemptAt is null || completedAt > previous.LastAttemptAt);
    }

    private static CollectionSnapshot Build(
        FrozenDictionary<Platform, PlatformCollectionState> states,
        DateTimeOffset observedAt)
    {
        return new CollectionSnapshot(
            LiveSnapshot.Create(states.Values.SelectMany(state => state.Streams), observedAt),
            states,
            observedAt);
    }
}
