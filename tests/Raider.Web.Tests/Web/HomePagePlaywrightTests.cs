// 실제 Chromium에서 홈 화면의 핵심 반응형 탐색 흐름을 검증한다.
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Raider.Web.Collection;
using Raider.Web.Live;

namespace Raider.Web.Tests.Web;

public sealed class HomePagePlaywrightTests
{
    [Fact]
    public async Task DesktopSearchAndMobileLayoutWorkInChromium()
    {
        await using var application = new TestApplicationFactory();
        application.UseKestrel(0);
        using var client = application.CreateClient();
        var snapshots = application.Services.GetRequiredService<SnapshotStore>();
        snapshots.ApplySuccess(
            Platform.Chzzk,
            [Stream("alpha", Platform.Chzzk, "Alpha", "Special Game", 100, ["game"])],
            DateTimeOffset.UtcNow);
        snapshots.ApplySuccess(
            Platform.Soop,
            [Stream("beta", Platform.Soop, "Beta", "Talk", 50, ["talk"])],
            DateTimeOffset.UtcNow.AddTicks(1));

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 1440, Height = 900 } });

        await page.GotoAsync(client.BaseAddress!.ToString());
        await Assertions.Expect(page.Locator(".stream-card")).ToHaveCountAsync(2);
        await page.GetByPlaceholder("방송인, 제목 또는 태그 검색").FillAsync("alpha");
        await page.GetByRole(AriaRole.Button, new() { Name = "검색", Exact = true }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"[?&]q=alpha"));
        await Assertions.Expect(page.Locator(".stream-card")).ToHaveCountAsync(1);

        await page.SetViewportSizeAsync(375, 812);
        await page.GotoAsync(client.BaseAddress.ToString());
        var layout = await page.EvaluateAsync<int[]>(
            "() => [document.documentElement.scrollWidth, window.innerWidth, getComputedStyle(document.querySelector('.stream-grid')).gridTemplateColumns.split(' ').length]");

        Assert.True(layout[0] <= layout[1], $"Expected no horizontal scroll, measured {layout[0]}px > {layout[1]}px.");
        Assert.Equal(1, layout[2]);
    }

    private static LiveStream Stream(
        string id,
        Platform platform,
        string streamer,
        string title,
        int viewers,
        IEnumerable<string> tags)
    {
        return LiveStream.Create(
            platform,
            id,
            $"channel-{id}",
            streamer,
            title,
            viewers,
            null,
            $"https://example.invalid/{id}",
            tags,
            DateTimeOffset.UtcNow);
    }
}
