// 치지직 OAuth URL과 코드 교환 계약을 검증한다.
using System.Net;
using Microsoft.Extensions.Options;
using Raider.Web.Configuration;
using Raider.Web.Recap;

namespace Raider.Web.Tests.Recap;

public sealed class ChzzkAuthClientTests
{
    [Fact]
    public void AuthorizationUrlIncludesClientRedirectAndState()
    {
        var client = new ChzzkAuthClient(
            new HttpClient(),
            Options.Create(new ChzzkOptions
            {
                ClientId = "id-1",
                ClientSecret = "secret",
                RedirectUri = "https://raider.example/auth/chzzk/callback",
            }));

        var url = client.CreateAuthorizationUrl("state-1");

        Assert.StartsWith("https://chzzk.naver.com/account-interlock?", url, StringComparison.Ordinal);
        Assert.Contains("clientId=id-1", url, StringComparison.Ordinal);
        Assert.Contains("redirectUri=https%3A%2F%2Fraider.example%2Fauth%2Fchzzk%2Fcallback", url, StringComparison.Ordinal);
        Assert.Contains("state=state-1", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExchangeReadsUserFromTokenAndMeEndpoints()
    {
        var handler = new FixtureHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("token", StringComparison.Ordinal))
            {
                Assert.Equal("id", Assert.Single(request.Headers.GetValues("Client-Id")));
                Assert.Equal("secret", Assert.Single(request.Headers.GetValues("Client-Secret")));
                return Json("""{"code":200,"content":{"accessToken":"access-1"}}""");
            }

            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("access-1", request.Headers.Authorization?.Parameter);
            return Json("""{"code":200,"content":{"channelId":"me-1","channelName":"Viewer"}}""");
        });
        var client = new ChzzkAuthClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://openapi.chzzk.naver.com/") },
            Options.Create(new ChzzkOptions { ClientId = "id", ClientSecret = "secret", RedirectUri = "https://localhost/callback" }));

        var user = await client.ExchangeAsync("code-1", "state-1", CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal("me-1", user.ChannelId);
        Assert.Equal("Viewer", user.ChannelName);
    }

    [Fact]
    public async Task ExchangeReturnsNullWhenOpenApiIsUnreachable()
    {
        var client = new ChzzkAuthClient(
            new HttpClient(new FailingHandler()) { BaseAddress = new Uri("https://openapi.chzzk.naver.com/") },
            Options.Create(new ChzzkOptions { ClientId = "id", ClientSecret = "secret", RedirectUri = "https://localhost/callback" }));

        Assert.Null(await client.ExchangeAsync("code-1", "state-1", CancellationToken.None));
    }

    private static HttpResponseMessage Json(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private sealed class FixtureHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Resource temporarily unavailable (openapi.chzzk.naver.com:443)");
    }
}
