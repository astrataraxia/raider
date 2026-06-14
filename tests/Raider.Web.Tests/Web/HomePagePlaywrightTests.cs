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
        await page.GetByRole(AriaRole.Button, new() { Name = "즐겨찾기 열기", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator(".favorites-sidebar")).ToHaveClassAsync(new Regex("is-mobile-open"));
        await page.SetViewportSizeAsync(1440, 900);
        Assert.False(await page.EvaluateAsync<bool>("() => document.querySelector('.page-shell').inert"));
        await page.SetViewportSizeAsync(375, 812);
        await page.GetByRole(AriaRole.Button, new() { Name = "즐겨찾기 열기", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "즐겨찾기 배경 닫기", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator(".favorites-sidebar")).Not.ToHaveClassAsync(new Regex("is-mobile-open"));
        var layout = await page.EvaluateAsync<int[]>(
            "() => [document.documentElement.scrollWidth, window.innerWidth, getComputedStyle(document.querySelector('.stream-grid')).gridTemplateColumns.split(' ').length]");

        Assert.True(layout[0] <= layout[1], $"Expected no horizontal scroll, measured {layout[0]}px > {layout[1]}px.");
        Assert.Equal(1, layout[2]);
    }

    [Fact]
    public async Task FavoriteIsSharedAcrossBrowserContextsAndSidebarCanCollapse()
    {
        await using var application = new TestApplicationFactory();
        application.UseKestrel(0);
        using var client = application.CreateClient();
        var snapshots = application.Services.GetRequiredService<SnapshotStore>();
        snapshots.ApplySuccess(
            Platform.Chzzk,
            [Stream("alpha", Platform.Chzzk, "Alpha", "Special Game", 100, ["game"])],
            DateTimeOffset.UtcNow);
        snapshots.ApplySuccess(Platform.Soop, [], DateTimeOffset.UtcNow.AddTicks(1));

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        await using var firstContext = await browser.NewContextAsync();
        var first = await firstContext.NewPageAsync();
        await first.GotoAsync(client.BaseAddress!.ToString());

        var geometry = await first.Locator(".favorite-toggle").First.EvaluateAsync<double[]>(
            """
            button => {
                const card = button.closest(".stream-card");
                const cardRect = card.getBoundingClientRect();
                const buttonRect = button.getBoundingClientRect();
                const iconRect = button.querySelector("svg").getBoundingClientRect();
                const overlapsTags = [...card.querySelectorAll(".card-tag-link")].some(tag => {
                    const tagsRect = tag.getBoundingClientRect();
                    return buttonRect.left < tagsRect.right &&
                        buttonRect.right > tagsRect.left &&
                        buttonRect.top < tagsRect.bottom &&
                        buttonRect.bottom > tagsRect.top;
                });

                return [buttonRect.width, iconRect.width, cardRect.right - buttonRect.right, overlapsTags ? 1 : 0];
            }
            """);

        Assert.True(geometry[0] >= 44);
        Assert.True(geometry[1] <= 20);
        Assert.InRange(geometry[2], 0, 2);
        Assert.Equal(0, geometry[3]);

        await first.GetByRole(AriaRole.Button, new() { Name = "Alpha 즐겨찾기 추가" }).ClickAsync();
        await Assertions.Expect(first.Locator(".favorite-item")).ToContainTextAsync("Alpha");
        await first.GetByRole(AriaRole.Button, new() { Name = "즐겨찾기 접기" }).ClickAsync();
        await Assertions.Expect(first.Locator(".favorites-sidebar")).ToHaveClassAsync(new Regex("is-collapsed"));

        await using var secondContext = await browser.NewContextAsync();
        var second = await secondContext.NewPageAsync();
        await second.GotoAsync(client.BaseAddress.ToString());
        await Assertions.Expect(second.Locator(".favorite-item")).ToContainTextAsync("Alpha");
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
