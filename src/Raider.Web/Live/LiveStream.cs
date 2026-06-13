// 플랫폼과 무관한 현재 라이브 방송 정보를 표현한다.
using System.Collections.Immutable;
using System.Text;

namespace Raider.Web.Live;

public sealed record LiveStream
{
    private LiveStream(
        Platform platform,
        string broadcastId,
        string channelId,
        string streamerName,
        string title,
        int viewerCount,
        Uri? thumbnailUrl,
        Uri watchUrl,
        ImmutableArray<string> tags,
        string searchText,
        DateTimeOffset observedAt)
    {
        Platform = platform;
        BroadcastId = broadcastId;
        ChannelId = channelId;
        StreamerName = streamerName;
        Title = title;
        ViewerCount = viewerCount;
        ThumbnailUrl = thumbnailUrl;
        WatchUrl = watchUrl;
        Tags = tags;
        SearchText = searchText;
        ObservedAt = observedAt;
    }

    public Platform Platform { get; }

    public string BroadcastId { get; }

    public string ChannelId { get; }

    public string StreamerName { get; }

    public string Title { get; }

    public int ViewerCount { get; }

    public Uri? ThumbnailUrl { get; }

    public Uri WatchUrl { get; }

    public ImmutableArray<string> Tags { get; }

    public string SearchText { get; }

    public DateTimeOffset ObservedAt { get; }

    public static LiveStream Create(
        Platform platform,
        string broadcastId,
        string channelId,
        string streamerName,
        string title,
        int viewerCount,
        string? thumbnailUrl,
        string watchUrl,
        IEnumerable<string> tags,
        DateTimeOffset observedAt)
    {
        if (!Enum.IsDefined(platform))
        {
            throw new ArgumentOutOfRangeException(nameof(platform));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(viewerCount);
        ArgumentNullException.ThrowIfNull(tags);

        var normalizedStreamerName = NormalizeRequired(streamerName, nameof(streamerName));
        var normalizedTitle = NormalizeRequired(title, nameof(title));
        var normalizedTags = NormalizeTags(tags);

        return new LiveStream(
            platform,
            NormalizeRequired(broadcastId, nameof(broadcastId)),
            NormalizeRequired(channelId, nameof(channelId)),
            normalizedStreamerName,
            normalizedTitle,
            viewerCount,
            ParseOptionalHttpUrl(thumbnailUrl),
            ParseRequiredHttpUrl(watchUrl, nameof(watchUrl)),
            normalizedTags,
            BuildSearchText(normalizedStreamerName, normalizedTitle, normalizedTags),
            observedAt);
    }

    public static ImmutableArray<LiveStream> OrderAndDeduplicate(IEnumerable<LiveStream> streams)
    {
        ArgumentNullException.ThrowIfNull(streams);

        var unique = new Dictionary<(Platform Platform, string BroadcastId), LiveStream>();
        foreach (var stream in streams)
        {
            ArgumentNullException.ThrowIfNull(stream);
            var key = (stream.Platform, stream.BroadcastId);

            if (!unique.TryGetValue(key, out var current) || IsPreferred(stream, current))
            {
                unique[key] = stream;
            }
        }

        return unique.Values
            .OrderByDescending(stream => stream.ViewerCount)
            .ThenBy(stream => stream.Platform)
            .ThenBy(stream => stream.BroadcastId, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool IsPreferred(LiveStream candidate, LiveStream current)
    {
        if (candidate.ViewerCount != current.ViewerCount)
        {
            return candidate.ViewerCount > current.ViewerCount;
        }

        if (candidate.ObservedAt != current.ObservedAt)
        {
            return candidate.ObservedAt > current.ObservedAt;
        }

        return string.CompareOrdinal(candidate.Title, current.Title) < 0;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim().Normalize(NormalizationForm.FormC);
    }

    private static ImmutableArray<string> NormalizeTags(IEnumerable<string> tags)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = ImmutableArray.CreateBuilder<string>();

        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            var value = tag.Trim().Normalize(NormalizationForm.FormC);
            if (unique.Add(value))
            {
                normalized.Add(value);
            }
        }

        return normalized.ToImmutable();
    }

    private static string BuildSearchText(string streamerName, string title, ImmutableArray<string> tags)
    {
        return NormalizeSearch(string.Join('\n', new[] { streamerName, title }.Concat(tags)));
    }

    internal static string NormalizeSearch(string value)
    {
        return value.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    private static Uri? ParseOptionalHttpUrl(string? value)
    {
        return TryParseHttpUrl(value, out var uri) ? uri : null;
    }

    private static Uri ParseRequiredHttpUrl(string value, string parameterName)
    {
        if (!TryParseHttpUrl(value, out var uri))
        {
            throw new ArgumentException("An absolute HTTP or HTTPS URL is required.", parameterName);
        }

        return uri;
    }

    private static bool TryParseHttpUrl(string? value, out Uri uri)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out uri!)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
