// SOOP 공개 웹 JSON client의 변환, 전체 페이지, 무쿠키, 오류 계약을 검증한다.
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Raider.Web.Collection;
using Raider.Web.Live;
using Raider.Web.Soop;

namespace Raider.Web.Tests.Soop;

public sealed class SoopClientTests
{
    [Fact]
    public async Task NormalFixtureMapsCurrentViewCountTagsAndUrls()
    {
        var client = CreateClient(new FixtureHandler(_ => Response("normal.json")));

        var result = await client.CollectAsync(CancellationToken.None);

        var stream = Assert.Single(result);
        Assert.Equal(Platform.Soop, stream.Platform);
        Assert.Equal("2001", stream.BroadcastId);
        Assert.Equal(321, stream.ViewerCount);
        Assert.Equal("https://example.invalid/soop/thumbnail-1.jpg", stream.ThumbnailUrl?.AbsoluteUri);
        Assert.Equal("https://play.sooplive.co.kr/fixture-user-1/2001", stream.WatchUrl.AbsoluteUri);
        Assert.Equal(
            ["Fixture auto", "Fixture category tag", "Fixture hash", "Fixture language", "Fixture category"],
            stream.Tags.ToArray());
    }

    [Fact]
    public async Task MissingOptionalThumbnailIsAllowedAndMissingRequiredStreamIsExcluded()
    {
        var optional = CreateClient(new FixtureHandler(_ => JsonResponse(
            """
            {"total_cnt":1,"cnt":1,"broad":[{"broad_no":2004,"user_id":"user","user_nick":"Streamer","broad_title":"Valid","broad_thumb":null,"current_view_cnt":1}],"time":0,"is_wp":0}
            """)));
        var missing = CreateClient(new FixtureHandler(_ => Response("missing-required-field.json")));

        Assert.Null(Assert.Single(await optional.CollectAsync(CancellationToken.None)).ThumbnailUrl);
        Assert.Empty(await missing.CollectAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UsesBroadStartFollowsPagesDeduplicatesAndSendsNoCookies()
    {
        var requests = new List<HttpRequestMessage>();
        var handler = new FixtureHandler(request =>
        {
            requests.Add(request);
            return requests.Count == 1
                ? Response("pagination-first.json")
                : JsonResponse(
                    """
                    {"total_cnt":61,"cnt":2,"broad":[{"broad_no":2001,"user_id":"fixture-user-1","user_nick":"Duplicate","broad_title":"Duplicate","current_view_cnt":1},{"broad_no":2002,"user_id":"fixture-user-2","user_nick":"Fixture streamer two","broad_title":"Fixture live two","current_view_cnt":123}],"time":0,"is_wp":0}
                    """);
        });

        var result = await CreateClient(handler).CollectAsync(CancellationToken.None);

        Assert.Equal(2, result.Length);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request =>
        {
            Assert.Contains("orderType=broad_start", request.RequestUri!.Query, StringComparison.Ordinal);
            Assert.False(request.Headers.Contains("Cookie"));
        });
        Assert.Contains("pageNo=1", requests[0].RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("pageNo=2", requests[1].RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaterPageFailureDiscardsPartialResult()
    {
        var count = 0;
        var handler = new FixtureHandler(_ =>
        {
            count++;
            return count == 1 ? Response("pagination-first.json") : new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        await Assert.ThrowsAsync<PlatformCollectionException>(
            () => CreateClient(handler).CollectAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, PlatformErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.Forbidden, PlatformErrorKind.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, PlatformErrorKind.Server)]
    public async Task MapsBlockingAndServerErrors(HttpStatusCode status, PlatformErrorKind expected)
    {
        var error = await Assert.ThrowsAsync<PlatformCollectionException>(
            () => CreateClient(new FixtureHandler(_ => new HttpResponseMessage(status))).CollectAsync(CancellationToken.None));

        Assert.Equal(expected, error.Error.Kind);
    }

    [Fact]
    public async Task ContractChangeAndHtmlBlockAreNotEmptyLists()
    {
        var changed = CreateClient(new FixtureHandler(_ => JsonResponse("""{"unexpected":true}""")));
        var blocked = CreateClient(new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>blocked</html>"),
        }));

        var changedError = await Assert.ThrowsAsync<PlatformCollectionException>(
            () => changed.CollectAsync(CancellationToken.None));
        var blockedError = await Assert.ThrowsAsync<PlatformCollectionException>(
            () => blocked.CollectAsync(CancellationToken.None));

        Assert.Equal(PlatformErrorKind.Contract, changedError.Error.Kind);
        Assert.Equal(PlatformErrorKind.Contract, blockedError.Error.Kind);
    }

    [Fact]
    public async Task MapsTimeout()
    {
        var client = CreateClient(new AsyncFixtureHandler((_, _) => throw new TaskCanceledException()));

        var error = await Assert.ThrowsAsync<PlatformCollectionException>(
            () => client.CollectAsync(CancellationToken.None));

        Assert.Equal(PlatformErrorKind.Timeout, error.Error.Kind);
    }

    private static SoopClient CreateClient(HttpMessageHandler handler)
    {
        return new SoopClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://live.sooplive.com/"),
                Timeout = TimeSpan.FromSeconds(5),
            },
            TimeProvider.System,
            NullLogger<SoopClient>.Instance);
    }

    private static HttpResponseMessage Response(string fixtureName)
    {
        return JsonResponse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Soop", fixtureName)));
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        };
    }

    private sealed class FixtureHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(respond(request));
        }
    }

    private sealed class AsyncFixtureHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return respond(request, cancellationToken);
        }
    }
}
