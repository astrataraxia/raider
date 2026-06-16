// 저장된 즐겨찾기와 현재 스냅샷을 결합해 라이브 상태를 제공한다.
using System.Collections.Immutable;
using Raider.Web.Collection;
using Raider.Web.Live;

namespace Raider.Web.Favorites;

public sealed class FavoriteCatalog(FavoriteStore store, SnapshotStore snapshots, TimeProvider timeProvider)
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(20);

    public async Task<ImmutableArray<FavoriteView>> ListAsync(CancellationToken cancellationToken)
    {
        var snapshot = snapshots.Current;
        var streams = snapshot.Live.Streams
            .GroupBy(stream => (stream.Platform, stream.ChannelId))
            .ToDictionary(group => group.Key, group => group.First());
        var favorites = await store.ListAsync(cancellationToken);

        return favorites
            .Select(favorite => Build(favorite, snapshot, streams))
            .OrderBy(view => StatusRank(view.Status))
            .ThenBy(view => view.StreamerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(view => view.Platform)
            .ThenBy(view => view.ChannelId, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static int StatusRank(string status)
    {
        return status switch
        {
            "live" => 0,
            "offline" => 1,
            _ => 2,
        };
    }

    public LiveStream? FindCurrent(Platform platform, string channelId)
    {
        return snapshots.Current.Live.Streams.FirstOrDefault(
            stream => stream.Platform == platform && string.Equals(stream.ChannelId, channelId, StringComparison.Ordinal));
    }

    private FavoriteView Build(
        Favorite favorite,
        CollectionSnapshot snapshot,
        Dictionary<(Platform Platform, string ChannelId), LiveStream> streams)
    {
        var state = snapshot.Platforms[favorite.Platform];
        var isDelayed = state.IsPartial
            || state.Error is not null
            || state.LastSuccessAt is null
            || timeProvider.GetUtcNow() - state.LastSuccessAt > StaleAfter;
        streams.TryGetValue((favorite.Platform, favorite.ChannelId), out var stream);
        var status = isDelayed ? FavoriteStatus.Delayed : stream is null ? FavoriteStatus.Offline : FavoriteStatus.Live;

        return new FavoriteView(
            FavoriteStore.FormatPlatform(favorite.Platform),
            favorite.ChannelId,
            favorite.StreamerName,
            status.ToString().ToLowerInvariant(),
            status == FavoriteStatus.Live ? stream?.WatchUrl : null,
            status == FavoriteStatus.Live ? stream?.ViewerCount : null,
            favorite.Category);
    }
}

public sealed record FavoriteView(
    string Platform,
    string ChannelId,
    string StreamerName,
    string Status,
    string? WatchUrl,
    int? ViewerCount,
    string Category);

internal enum FavoriteStatus
{
    Live,
    Delayed,
    Offline,
}
