// 라이브 홈 화면의 타일, 필터, 검색, 상태 HTML 계약을 검증한다.
using System.Collections.Immutable;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Raider.Web.Collection;
using Raider.Web.Live;

namespace Raider.Web.Tests.Web;

public sealed class HomePageTests : IDisposable
{
    private readonly WebApplicationFactory<Program> application;
    private readonly HttpClient client;
    private readonly SnapshotStore snapshots;

    public HomePageTests()
    {
        application = new TestApplicationFactory();
        client = application.CreateClient();
        snapshots = application.Services.GetRequiredService<SnapshotStore>();
    }

    [Fact]
    public async Task TileShowsRequiredInformationAndAtMostThreeTags()
    {
        snapshots.ApplySuccess(
            Platform.Chzzk,
            [Stream("alpha", Platform.Chzzk, "Alpha", "Special Title", 321, ["one", "two", "three", "four"])],
            DateTimeOffset.UtcNow);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/", CancellationToken.None));

        Assert.Contains("CHZZK", html, StringComparison.Ordinal);
        Assert.Contains("Alpha", html, StringComparison.Ordinal);
        Assert.Contains("Special Title", html, StringComparison.Ordinal);
        Assert.Contains("321", html, StringComparison.Ordinal);
        Assert.Contains("https://example.invalid/alpha", html, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"Alpha의 방송 썸네일\"", html, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer\"", html, StringComparison.Ordinal);
        Assert.Contains(">one<", html, StringComparison.Ordinal);
        Assert.Contains(">three<", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">four<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryCombinesPlatformTagAndSearchAndKeepsValues()
    {
        snapshots.ApplySuccess(
            Platform.Chzzk,
            [Stream("alpha", Platform.Chzzk, "Alpha", "Horror", 30, ["game"])],
            DateTimeOffset.UtcNow);
        snapshots.ApplySuccess(
            Platform.Soop,
            [Stream("beta", Platform.Soop, "Beta", "Horror", 20, ["game"])],
            DateTimeOffset.UtcNow.AddTicks(1));

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/?platform=soop&tag=game&q=beta", CancellationToken.None));

        Assert.Contains("data-broadcast-id=\"beta\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-broadcast-id=\"alpha\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"beta\"", html, StringComparison.Ordinal);
        Assert.Contains("data-selected-tag=\"game\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrdersByViewerCountAndEncodesExternalText()
    {
        snapshots.ApplySuccess(
            Platform.Chzzk,
            [
                Stream("low", Platform.Chzzk, "Low", "<script>alert(1)</script>", 1, []),
                Stream("high", Platform.Chzzk, "High", "Popular", 100, []),
            ],
            DateTimeOffset.UtcNow);

        var rawHtml = await client.GetStringAsync("/", CancellationToken.None);

        Assert.True(
            rawHtml.IndexOf("data-broadcast-id=\"high\"", StringComparison.Ordinal)
            < rawHtml.IndexOf("data-broadcast-id=\"low\"", StringComparison.Ordinal));
        Assert.DoesNotContain("<script>alert(1)</script>", rawHtml, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", rawHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShowsInitialAndPartialFailureStates()
    {
        var initial = WebUtility.HtmlDecode(await client.GetStringAsync("/", CancellationToken.None));
        snapshots.ApplyFailure(Platform.Chzzk, new PlatformError(PlatformErrorKind.Server), DateTimeOffset.UtcNow);
        snapshots.ApplySuccess(Platform.Soop, [Stream("soop", Platform.Soop, "Soop", "Live", 1, [])], DateTimeOffset.UtcNow.AddTicks(1));

        var partial = WebUtility.HtmlDecode(await client.GetStringAsync("/", CancellationToken.None));

        Assert.Contains("첫 방송 목록을 수집하고 있습니다", initial, StringComparison.Ordinal);
        Assert.Contains("일부 플랫폼 갱신이 지연되고 있습니다", partial, StringComparison.Ordinal);
        Assert.Contains("data-broadcast-id=\"soop\"", partial, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShowsDefaultThumbnailEmptyAndStaleStates()
    {
        snapshots.ApplySuccess(
            Platform.Chzzk,
            [Stream("no-image", Platform.Chzzk, "No Image", "Live", 1, [], thumbnailUrl: null)],
            DateTimeOffset.UtcNow.AddHours(-1));
        snapshots.ApplySuccess(Platform.Soop, [], DateTimeOffset.UtcNow.AddHours(-1).AddTicks(1));

        var stale = WebUtility.HtmlDecode(await client.GetStringAsync("/", CancellationToken.None));
        var empty = WebUtility.HtmlDecode(await client.GetStringAsync("/?q=not-found", CancellationToken.None));

        Assert.Contains("data-default-thumbnail=\"true\"", stale, StringComparison.Ordinal);
        Assert.Contains("No Image의 방송 썸네일", stale, StringComparison.Ordinal);
        Assert.Contains("방송 목록이 오래되었습니다", stale, StringComparison.Ordinal);
        Assert.Contains("현재 방송을 찾지 못했습니다.", empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PaginatesLargeResultsAndKeepsFilterQuery()
    {
        var streams = Enumerable.Range(0, 125)
            .Select(index => Stream($"stream-{index}", Platform.Chzzk, $"Streamer {index}", "Game", 125 - index, ["game"]))
            .ToImmutableArray();
        snapshots.ApplySuccess(Platform.Chzzk, streams, DateTimeOffset.UtcNow);

        var firstPage = WebUtility.HtmlDecode(await client.GetStringAsync("/?platform=chzzk&tag=game&q=streamer", CancellationToken.None));
        var secondPage = WebUtility.HtmlDecode(await client.GetStringAsync("/?platform=chzzk&tag=game&q=streamer&p=2", CancellationToken.None));

        Assert.Contains("data-broadcast-id=\"stream-0\"", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-broadcast-id=\"stream-124\"", firstPage, StringComparison.Ordinal);
        Assert.Contains("platform=chzzk", firstPage, StringComparison.Ordinal);
        Assert.Contains("tag=game", firstPage, StringComparison.Ordinal);
        Assert.Contains("q=streamer", firstPage, StringComparison.Ordinal);
        Assert.Contains("p=2", firstPage, StringComparison.Ordinal);
        Assert.Contains("data-broadcast-id=\"stream-124\"", secondPage, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        client.Dispose();
        application.Dispose();
    }

    private static LiveStream Stream(
        string id,
        Platform platform,
        string streamer,
        string title,
        int viewers,
        IEnumerable<string> tags,
        string? thumbnailUrl = "https://example.invalid/thumbnail.jpg")
    {
        return LiveStream.Create(
            platform,
            id,
            $"channel-{id}",
            streamer,
            title,
            viewers,
            thumbnailUrl,
            $"https://example.invalid/{id}",
            tags,
            DateTimeOffset.UtcNow);
    }
}
