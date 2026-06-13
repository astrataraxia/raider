// 현재 라이브 스냅샷의 태그 인덱스와 검색 계약을 검증한다.
using Raider.Web.Live;

namespace Raider.Web.Tests.Live;

public sealed class LiveSnapshotTests
{
    [Theory]
    [InlineData("alpha", "alpha")]
    [InlineData("special title", "alpha")]
    [InlineData("공포", "alpha")]
    public void SearchMatchesStreamerTitleAndTag(string query, string expectedBroadcastId)
    {
        var snapshot = CreateSnapshot();

        var result = snapshot.Search(query: query);

        Assert.Single(result);
        Assert.Equal(expectedBroadcastId, result[0].BroadcastId);
    }

    [Fact]
    public void SearchCombinesPlatformAndTagFilters()
    {
        var snapshot = CreateSnapshot();

        var result = snapshot.Search(Platform.Soop, "game");

        Assert.Single(result);
        Assert.Equal("beta", result[0].BroadcastId);
        Assert.Equal([0, 1], snapshot.StreamsByTag["game"].ToArray());
    }

    [Fact]
    public void SearchTreatsBlankQueryAsAllAndUnknownTagAsEmpty()
    {
        var snapshot = CreateSnapshot();

        Assert.Equal(3, snapshot.Search(query: "  ").Length);
        Assert.Empty(snapshot.Search(tag: "missing"));
    }

    private static LiveSnapshot CreateSnapshot()
    {
        return LiveSnapshot.Create(
            [
                Create("alpha", Platform.Chzzk, "Alpha Streamer", "Special Title", 30, ["Game", "공포"]),
                Create("beta", Platform.Soop, "Beta Streamer", "Other", 20, ["game"]),
                Create("gamma", Platform.Chzzk, "Gamma Streamer", "Chat", 10, ["Talk"]),
            ],
            new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero));
    }

    private static LiveStream Create(
        string broadcastId,
        Platform platform,
        string streamerName,
        string title,
        int viewerCount,
        IEnumerable<string> tags)
    {
        return LiveStream.Create(
            platform,
            broadcastId,
            $"channel-{broadcastId}",
            streamerName,
            title,
            viewerCount,
            null,
            $"https://example.invalid/{broadcastId}",
            tags,
            new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero));
    }
}
