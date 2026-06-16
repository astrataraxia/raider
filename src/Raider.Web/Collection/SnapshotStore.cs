// 플랫폼 결과를 병합해 완성된 최신 스냅샷 참조만 원자 교체한다.
using System.Collections.Frozen;
using System.Collections.Immutable;
using Raider.Web.Live;

namespace Raider.Web.Collection;

public sealed class SnapshotStore
{
    private readonly object updateLock = new();
    private readonly Dictionary<Platform, ImmutableArray<LiveStream>> completeStreams;
    private CollectionSnapshot current;

    public SnapshotStore(IEnumerable<Platform> platforms)
    {
        var states = platforms
            .Distinct()
            .ToFrozenDictionary(
                platform => platform,
                platform => new PlatformCollectionState(platform, [], null, null, null, false));
        completeStreams = states.Keys.ToDictionary(platform => platform, _ => ImmutableArray<LiveStream>.Empty);
        current = Build(states, DateTimeOffset.MinValue, 0);
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
            states[platform] = new PlatformCollectionState(platform, streams, completedAt, completedAt, null, false);
            completeStreams[platform] = streams;
            Interlocked.Exchange(ref current, Build(states.ToFrozenDictionary(), completedAt, snapshot.Version + 1));
            return true;
        }
    }

    public void ApplyPartial(Platform platform, ImmutableArray<LiveStream> streams, DateTimeOffset observedAt)
    {
        lock (updateLock)
        {
            var snapshot = Current;
            var previous = snapshot.Platforms[platform];
            var states = snapshot.Platforms.ToDictionary();
            states[platform] = previous with
            {
                Streams = streams,
                IsPartial = true,
            };
            Interlocked.Exchange(ref current, Build(states.ToFrozenDictionary(), observedAt, snapshot.Version + 1));
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
                Streams = completeStreams[platform],
                LastAttemptAt = completedAt,
                Error = error,
                IsPartial = false,
            };
            Interlocked.Exchange(ref current, Build(states.ToFrozenDictionary(), completedAt, snapshot.Version + 1));
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
        DateTimeOffset observedAt,
        long version)
    {
        return new CollectionSnapshot(
            LiveSnapshot.Create(states.Values.SelectMany(state => state.Streams), observedAt),
            states,
            observedAt,
            version);
    }
}
