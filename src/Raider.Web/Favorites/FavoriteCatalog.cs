// 저장된 즐겨찾기와 현재 스냅샷을 결합해 라이브 상태를 제공한다.
using System.Collections.Immutable;
using Raider.Web.Collection;
using Raider.Web.Live;

namespace Raider.Web.Favorites;

public sealed class FavoriteCatalog(FavoriteStore store, SnapshotStore snapshots, TimeProvider timeProvider)
{

    public async Task<ImmutableArray<FavoriteView>> ListAsync(CancellationToken cancellationToken)
    {
        var snapshot = snapshots.Current;
        var streams = snapshot.Platforms.Values
            .SelectMany(state => state.IsPartial ? state.SettledStreams : state.Streams)
            .GroupBy(stream => (stream.Platform, stream.ChannelId))
            .ToDictionary(group => group.Key, group => group.First());
        var favorites = await store.ListAsync(cancellationToken);

        return favorites
            .Select(favorite => Build(favorite, snapshot, streams))
            .OrderBy(view => view.Status == "live" ? 0 : view.Status == "offline" ? 1 : 2)
            .ThenBy(view => view.StreamerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(view => view.Platform)
            .ThenBy(view => view.ChannelId, StringComparer.Ordinal)
            .ToImmutableArray();
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
        var isDelayed = !state.IsPartial && (
            state.Error is not null
            || state.LastSuccessAt is null
            || timeProvider.GetUtcNow() - state.LastSuccessAt > CollectionSnapshot.StaleAfter);
        streams.TryGetValue((favorite.Platform, favorite.ChannelId), out var stream);
        var status = isDelayed ? "delayed" : stream is null ? "offline" : "live";

        return new FavoriteView(
            FavoriteDbContext.FormatPlatform(favorite.Platform),
            favorite.ChannelId,
            favorite.StreamerName,
            status,
            stream?.WatchUrl ?? ChannelPage(favorite.Platform, favorite.ChannelId),
            status == "live" ? stream?.ViewerCount : null,
            favorite.Category);
    }

    private static string? ChannelPage(Platform platform, string channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return null;
        }

        return platform switch
        {
            Platform.Chzzk => $"https://chzzk.naver.com/live/{channelId}",
            Platform.Soop => $"https://www.sooplive.com/station/{channelId}",
            _ => null,
        };
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
