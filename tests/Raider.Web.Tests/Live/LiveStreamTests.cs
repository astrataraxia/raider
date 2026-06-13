// 공통 라이브 모델의 검증, 태그 정규화, 정렬 계약을 검증한다.
using Raider.Web.Live;

namespace Raider.Web.Tests.Live;

public sealed class LiveStreamTests
{
    [Fact]
    public void CreateValidatesRequiredFieldsUrlsAndViewerCount()
    {
        var stream = Create(thumbnailUrl: "not-a-url");

        Assert.Equal("broadcast-1", stream.BroadcastId);
        Assert.Null(stream.ThumbnailUrl);
        Assert.Throws<ArgumentOutOfRangeException>(() => Create((Platform)999));
        Assert.Throws<ArgumentException>(() => Create(broadcastId: " "));
        Assert.Throws<ArgumentException>(() => Create(channelId: " "));
        Assert.Throws<ArgumentException>(() => Create(streamerName: " "));
        Assert.Throws<ArgumentException>(() => Create(title: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(viewerCount: -1));
        Assert.Throws<ArgumentException>(() => Create(watchUrl: "ftp://example.com/live"));
    }

    [Fact]
    public void CreateNormalizesTagsAndSearchText()
    {
        var stream = Create(
            streamerName: " Streamer ",
            title: " Title ",
            tags: ["  Cafe\u0301 ", "CAFÉ", " ", "Game"]);

        Assert.Equal(["Café", "Game"], stream.Tags.ToArray());
        Assert.Equal("STREAMER\nTITLE\nCAFÉ\nGAME", stream.SearchText);
    }

    [Fact]
    public void OrderAndDeduplicateUsesPlatformBroadcastIdentityAndDeterministicOrder()
    {
        var streams = new[]
        {
            Create(Platform.Soop, "same", viewerCount: 20),
            Create(Platform.Chzzk, "same", viewerCount: 10),
            Create(Platform.Chzzk, "duplicate", viewerCount: 5),
            Create(Platform.Chzzk, "duplicate", viewerCount: 30),
            Create(Platform.Chzzk, "alpha", viewerCount: 20),
        };

        var result = LiveStream.OrderAndDeduplicate(streams);

        Assert.Equal(4, result.Length);
        Assert.Equal(
            [
                (Platform.Chzzk, "duplicate", 30),
                (Platform.Chzzk, "alpha", 20),
                (Platform.Soop, "same", 20),
                (Platform.Chzzk, "same", 10),
            ],
            result.Select(stream => (stream.Platform, stream.BroadcastId, stream.ViewerCount)));
    }

    private static LiveStream Create(
        Platform platform = Platform.Chzzk,
        string broadcastId = "broadcast-1",
        string channelId = "channel-1",
        string streamerName = "Streamer",
        string title = "Title",
        int viewerCount = 10,
        string? thumbnailUrl = "https://example.invalid/thumbnail.jpg",
        string watchUrl = "https://example.invalid/live",
        IEnumerable<string>? tags = null)
    {
        return LiveStream.Create(
            platform,
            broadcastId,
            channelId,
            streamerName,
            title,
            viewerCount,
            thumbnailUrl,
            watchUrl,
            tags ?? [],
            new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero));
    }
}
