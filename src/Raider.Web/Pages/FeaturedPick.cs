// 홈 피처드 카드에 그릴 즐겨찾기 라이브 한 건이다.
using System.Collections.Immutable;
using Raider.Web.Favorites;
using Raider.Web.Live;

namespace Raider.Web.Pages;

public sealed record FeaturedPick(
    string Platform,
    string PlatformLabel,
    string ChannelId,
    string StreamerName,
    string Title,
    int ViewerCount,
    string? ThumbnailUrl,
    string WatchUrl,
    string Line,
    ImmutableArray<string> Tags)
{
    public static ImmutableArray<FeaturedPick> FromSnapshot(ImmutableArray<LiveStream> streams, ImmutableArray<Favorite> favorites)
    {
        if (streams.IsDefaultOrEmpty || favorites.IsDefaultOrEmpty)
        {
            return [];
        }

        var byChannel = new Dictionary<(Platform Platform, string ChannelId), Favorite>();
        foreach (var favorite in favorites)
        {
            byChannel.TryAdd((favorite.Platform, favorite.ChannelId), favorite);
        }

        return streams
            .Where(stream => byChannel.ContainsKey((stream.Platform, stream.ChannelId)))
            .OrderByDescending(stream => stream.ViewerCount)
            .ThenBy(stream => stream.StreamerName, StringComparer.Ordinal)
            .ThenBy(stream => stream.ChannelId, StringComparer.Ordinal)
            .Select(stream =>
            {
                var favorite = byChannel[(stream.Platform, stream.ChannelId)];
                var line = stream.Tags.Length > 0 ? stream.Tags[0] : favorite.Category;
                return new FeaturedPick(
                    FavoriteStore.FormatPlatform(stream.Platform),
                    stream.Platform switch
                    {
                        Raider.Web.Live.Platform.Chzzk => "CHZZK",
                        Raider.Web.Live.Platform.Soop => "SOOP",
                        _ => stream.Platform.ToString(),
                    },
                    stream.ChannelId,
                    stream.StreamerName,
                    stream.Title,
                    stream.ViewerCount,
                    stream.ThumbnailUrl,
                    stream.WatchUrl,
                    line,
                    stream.Tags);
            })
            .ToImmutableArray();
    }
}
