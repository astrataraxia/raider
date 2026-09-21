// 리캡 로그인 흐름과 화면 계약을 검증한다.
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Raider.Web.Configuration;
using Raider.Web.Favorites;
using Raider.Web.Live;
using Raider.Web.Recap;

namespace Raider.Web.Tests.Recap;

public sealed class RecapPageTests
{
    [Fact]
    public async Task HomeIncludesRecapLink()
    {
        using var application = new TestApplicationFactory();
        using var client = application.CreateClient();

        var html = await client.GetStringAsync("/", CancellationToken.None);

        Assert.Contains("/Recap", html, StringComparison.Ordinal);
        Assert.Contains("리캡", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecapWithoutLoginShowsLoginLinkAndDoesNotRedirect()
    {
        using var application = new TestApplicationFactory();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/recap", CancellationToken.None);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("/auth/chzzk", html, StringComparison.Ordinal);
        Assert.Contains("치지직으로 로그인", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallbackAcceptsIssuedStateWithoutCookie()
    {
        using var application = new RecapAuthFactory();
        var states = application.Services.GetRequiredService<OauthStateStore>();
        var state = states.Issue();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        using var callback = await client.GetAsync($"/auth/chzzk/callback?code=code-1&state={state}", CancellationToken.None);

        Assert.Equal("/recap", callback.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task FailedCallbackStaysOnRecapWithoutStartingOauthAgain()
    {
        using var application = new TestApplicationFactory();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/auth/chzzk/callback?code=bad&state=bad", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/recap?login=failed", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AuthStartRedirectsToChzzkWithState()
    {
        using var application = new TestApplicationFactory();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/auth/chzzk", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location).ToString();
        Assert.StartsWith("https://chzzk.naver.com/account-interlock?", location, StringComparison.Ordinal);
        Assert.Contains("clientId=fixture-client-id", location, StringComparison.Ordinal);
        Assert.Contains("state=", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallbackSignsInAndRecapShowsCountsWithoutPyramid()
    {
        using var application = new RecapAuthFactory();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var store = application.Services.GetRequiredService<ChatCountStore>();
        var favorites = application.Services.GetRequiredService<FavoriteStore>();
        await favorites.UpsertAsync(new Favorite(Platform.Chzzk, "home", "Home"), CancellationToken.None);
        await store.AddChatAsync("home", "me-1", new DateOnly(2026, 9, 1), CancellationToken.None);
        await store.AddChatAsync("home", "me-1", new DateOnly(2026, 9, 1), CancellationToken.None);

        using var start = await client.GetAsync("/auth/chzzk", CancellationToken.None);
        var location = Assert.IsType<Uri>(start.Headers.Location);
        var state = QueryHelpers.ParseQuery(location.Query)["state"].ToString();
        using var callback = await client.GetAsync($"/auth/chzzk/callback?code=code-1&state={state}", CancellationToken.None);
        Assert.Equal("/recap", callback.Headers.Location?.OriginalString);

        using var recap = await client.GetAsync("/recap", CancellationToken.None);
        recap.EnsureSuccessStatusCode();
        var html = await recap.Content.ReadAsStringAsync();

        Assert.Contains("Viewer", html, StringComparison.Ordinal);
        Assert.Contains("본진", html, StringComparison.Ordinal);
        Assert.Contains("총 채팅", html, StringComparison.Ordinal);
        Assert.Contains("2건", html, StringComparison.Ordinal);
        Assert.DoesNotContain("피라미드", html, StringComparison.Ordinal);
        Assert.DoesNotContain("N명 중", html, StringComparison.Ordinal);
    }

    private sealed class RecapAuthFactory : TestApplicationFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ChzzkAuthClient>();
                services.AddSingleton(new ChzzkAuthClient(
                    new HttpClient(new AuthHandler()) { BaseAddress = new Uri("https://openapi.chzzk.naver.com/") },
                    Options.Create(new ChzzkOptions
                    {
                        ClientId = "fixture-client-id",
                        ClientSecret = "fixture-client-secret",
                        RedirectUri = "https://localhost/auth/chzzk/callback",
                    })));
            });
        }
    }

    private sealed class AuthHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = request.RequestUri!.AbsolutePath.Contains("token", StringComparison.Ordinal)
                ? """{"content":{"accessToken":"access-1"}}"""
                : """{"content":{"channelId":"me-1","channelName":"Viewer"}}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
