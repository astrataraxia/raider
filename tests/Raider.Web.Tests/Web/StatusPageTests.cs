// 서버 상태 화면이 수집 실패, 이전 목록, 최근 경고를 그대로 보여 주는지 검증한다.
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Raider.Web.Collection;
using Raider.Web.Live;
using Raider.Web.Status;

namespace Raider.Web.Tests.Web;

public sealed class StatusPageTests : IDisposable
{
    private readonly WebApplicationFactory<Program> application;
    private readonly HttpClient client;
    private readonly SnapshotStore snapshots;

    public StatusPageTests()
    {
        application = new TestApplicationFactory();
        client = application.CreateClient();
        snapshots = application.Services.GetRequiredService<SnapshotStore>();
    }

    [Fact]
    public async Task ShowsWaitingStateAndLinksFromHome()
    {
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/status", CancellationToken.None));
        var home = WebUtility.HtmlDecode(await client.GetStringAsync("/", CancellationToken.None));
        var recap = WebUtility.HtmlDecode(await client.GetStringAsync("/recap", CancellationToken.None));

        Assert.Contains("서버 상태", html, StringComparison.Ordinal);
        Assert.Contains("첫 수집을 기다리는 중입니다.", html, StringComparison.Ordinal);
        Assert.Contains("채팅 수집이 꺼져 있습니다.", html, StringComparison.Ordinal);
        Assert.Contains("최근 경고가 없습니다.", html, StringComparison.Ordinal);
        Assert.Contains("1분 미만", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/status\"", home, StringComparison.Ordinal);
        Assert.Contains("href=\"/status\"", recap, StringComparison.Ordinal);
        Assert.Contains("href=\"/Recap\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShowsTimeoutWhileKeepingThePreviousCatalog()
    {
        var now = DateTimeOffset.UtcNow;
        snapshots.ApplySuccess(
            Platform.Chzzk,
            [Stream("alpha")],
            now,
            TimeSpan.FromMilliseconds(1800));
        snapshots.ApplySuccess(
            Platform.Soop,
            [Stream("beta"), Stream("gamma")],
            now.AddMinutes(-18).AddSeconds(-30),
            TimeSpan.FromSeconds(2));
        snapshots.ApplyFailure(
            Platform.Soop,
            new PlatformError(PlatformErrorKind.Timeout),
            now,
            TimeSpan.FromMinutes(3));

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/status", CancellationToken.None));

        Assert.Contains("일부 수집 지연", html, StringComparison.Ordinal);
        Assert.Contains("SOOP 수집이 시간 초과로 끝났습니다.", html, StringComparison.Ordinal);
        Assert.Contains("2 · 이전 성공분", html, StringComparison.Ordinal);
        Assert.Contains("18분 / 20분", html, StringComparison.Ordinal);
        Assert.Contains("3분에서 중단", html, StringComparison.Ordinal);
        Assert.Contains("방금 수집 · 1.8초", html, StringComparison.Ordinal);
        Assert.Contains(">Timeout<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("첫 수집을 기다리는 중입니다.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShowsRecentWarningWithoutTheExceptionText()
    {
        application.Services.GetRequiredService<StatusLog>().Add(
            DateTimeOffset.UtcNow,
            "SOOP",
            "collect",
            "Timeout");

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/status", CancellationToken.None));

        Assert.Contains("collect", html, StringComparison.Ordinal);
        Assert.Contains("Timeout", html, StringComparison.Ordinal);
        Assert.DoesNotContain("최근 경고가 없습니다.", html, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        client.Dispose();
        application.Dispose();
    }

    private static LiveStream Stream(string id)
        => LiveStream.Create(
            id.StartsWith('b') || id == "gamma" ? Platform.Soop : Platform.Chzzk,
            id,
            $"channel-{id}",
            id,
            "Live",
            1,
            "https://example.invalid/t.jpg",
            $"https://example.invalid/{id}",
            [],
            DateTimeOffset.UtcNow);
}
