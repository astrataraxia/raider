// CHZZK typed client의 변환, 페이지 순회, 인증, 오류 계약을 검증한다.
using System.Collections.Immutable;
using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Raider.Web.Chzzk;
using Raider.Web.Collection;
using Raider.Web.Configuration;
using Raider.Web.Live;

namespace Raider.Web.Tests.Chzzk;

public sealed class ChzzkClientTests
{
    [Fact]
    public async Task NormalFixtureMapsRequiredFieldsTagsAndCategory()
    {
        var handler = new FixtureHandler(_ => Response("normal.json"));
        var client = CreateClient(handler);

        var result = await client.CollectAsync(CancellationToken.None);

        var stream = Assert.Single(result);
        Assert.Equal(Platform.Chzzk, stream.Platform);
        Assert.Equal("1001", stream.BroadcastId);
        Assert.Equal("fixture-channel-1", stream.ChannelId);
        Assert.Equal("Fixture streamer one", stream.StreamerName);
        Assert.Equal("Fixture live one", stream.Title);
        Assert.Equal(321, stream.ViewerCount);
        Assert.Equal("https://example.invalid/chzzk/thumbnail-1.jpg", stream.ThumbnailUrl);
        Assert.Equal("https://chzzk.naver.com/live/fixture-channel-1", stream.WatchUrl);
        Assert.Equal(["Fixture tag 1", "Fixture tag 2", "Fixture category 1"], stream.Tags.ToArray());
    }

    [Fact]
    public async Task MissingOptionalThumbnailIsAllowedAndMissingRequiredStreamIsExcluded()
    {
        var optionalHandler = new FixtureHandler(_ => JsonResponse(
            """
            {"code":200,"message":null,"content":{"data":[{"liveId":1004,"liveTitle":"Valid","liveThumbnailImageUrl":null,"concurrentUserCount":1,"tags":[],"categoryType":"ETC","liveCategory":null,"liveCategoryValue":null,"channelId":"channel-4","channelName":"Streamer"}],"page":{"next":null}}}
            """));
        var missingHandler = new FixtureHandler(_ => Response("missing-required-field.json"));

        var optional = Assert.Single(await CreateClient(optionalHandler).CollectAsync(CancellationToken.None));
        var missing = await CreateClient(missingHandler).CollectAsync(CancellationToken.None);

        Assert.Null(optional.ThumbnailUrl);
        Assert.Empty(missing);
    }

    [Fact]
    public async Task ThumbnailTemplateUsesDisplaySize()
    {
        var handler = new FixtureHandler(_ => JsonResponse(
            """
            {"code":200,"message":null,"content":{"data":[{"liveId":1005,"liveTitle":"Valid","liveThumbnailImageUrl":"https://example.invalid/image_{type}.jpg","concurrentUserCount":1,"tags":[],"channelId":"channel-5","channelName":"Streamer"}],"page":{"next":null}}}
            """));

        var stream = Assert.Single(await CreateClient(handler).CollectAsync(CancellationToken.None));

        Assert.Equal("https://example.invalid/image_480.jpg", stream.ThumbnailUrl);
    }

    [Fact]
    public async Task MissingBroadcastIdIsExcluded()
    {
        var handler = new FixtureHandler(_ => JsonResponse(
            """
            {"code":200,"message":null,"content":{"data":[{"liveTitle":"Invalid","concurrentUserCount":1,"channelId":"channel","channelName":"Streamer"}],"page":{"next":null}}}
            """));

        var result = await CreateClient(handler).CollectAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FollowsCursorStopsAtLastPageAndDeduplicates()
    {
        var requests = new List<Uri>();
        var handler = new FixtureHandler(request =>
        {
            requests.Add(request.RequestUri!);
            return requests.Count == 1 ? Response("pagination-first.json") : Response("pagination-last.json");
        });

        var result = await CreateClient(handler).CollectAsync(CancellationToken.None);

        Assert.Equal(2, result.Length);
        Assert.Equal(2, requests.Count);
        Assert.DoesNotContain("next=", requests[0].Query, StringComparison.Ordinal);
        Assert.Contains("next=fixture-next-token", requests[1].Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishesFirstPageBeforeFollowingCursor()
    {
        var requests = 0;
        var handler = new FixtureHandler(_ =>
        {
            requests++;
            return requests == 1 ? Response("pagination-first.json") : Response("pagination-last.json");
        });
        var partial = ImmutableArray<LiveStream>.Empty;

        var result = await CreateClient(handler).CollectAsync(
            streams =>
            {
                partial = streams;
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.Single(partial);
        Assert.Equal(2, result.Length);
    }

    [Fact]
    public async Task SendsClientAuthenticationWithoutExposingItInErrors()
    {
        HttpRequestMessage? captured = null;
        var handler = new FixtureHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"code":401,"message":"INVALID_CLIENT"}"""),
            };
        });

        var error = await Assert.ThrowsAsync<PlatformCollectionException>(
            () => CreateClient(handler).CollectAsync(CancellationToken.None));

        Assert.Equal("fixture-client-id", captured!.Headers.GetValues("Client-Id").Single());
        Assert.Equal("fixture-client-secret", captured.Headers.GetValues("Client-Secret").Single());
        Assert.Equal("Raider/0.1", captured.Headers.UserAgent.ToString());
        Assert.Equal(PlatformErrorKind.Authentication, error.Error.Kind);
        Assert.DoesNotContain("fixture-client-id", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-client-secret", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, PlatformErrorKind.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests, PlatformErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, PlatformErrorKind.Server)]
    public async Task MapsHttpErrors(HttpStatusCode statusCode, PlatformErrorKind expected)
    {
        var handler = new FixtureHandler(_ => new HttpResponseMessage(statusCode));

        var error = await Assert.ThrowsAsync<PlatformCollectionException>(
            () => CreateClient(handler).CollectAsync(CancellationToken.None));

        Assert.Equal(expected, error.Error.Kind);
    }

    [Fact]
    public async Task DiscardsPartialResultWhenLaterPageFails()
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

    [Fact]
    public async Task MapsTimeoutAndInvalidContract()
    {
        var timeoutHandler = new AsyncFixtureHandler((_, _) => throw new TaskCanceledException());
        var invalidHandler = new FixtureHandler(_ => JsonResponse("""{"code":200,"content":null}"""));

        var timeout = await Assert.ThrowsAsync<PlatformCollectionException>(
            () => CreateClient(timeoutHandler).CollectAsync(CancellationToken.None));
        var invalid = await Assert.ThrowsAsync<PlatformCollectionException>(
            () => CreateClient(invalidHandler).CollectAsync(CancellationToken.None));

        Assert.Equal(PlatformErrorKind.Timeout, timeout.Error.Kind);
        Assert.Equal(PlatformErrorKind.Contract, invalid.Error.Kind);
    }

    [Fact]
    public async Task MissingCredentialsFailCollectionAsConfigurationWithoutSendingRequest()
    {
        var requested = false;
        var handler = new FixtureHandler(_ =>
        {
            requested = true;
            return JsonResponse("""{"code":200,"content":{"data":[],"page":{"next":null}}}""");
        });

        var error = await Assert.ThrowsAsync<PlatformCollectionException>(
            () => CreateClient(handler, new ChzzkOptions()).CollectAsync(CancellationToken.None));

        Assert.Equal(PlatformErrorKind.Configuration, error.Error.Kind);
        Assert.False(requested);
    }

    private static ChzzkClient CreateClient(HttpMessageHandler handler, ChzzkOptions? options = null)
    {
        return new ChzzkClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://openapi.chzzk.naver.com/"),
                Timeout = TimeSpan.FromSeconds(5),
            },
            Options.Create(options ?? new ChzzkOptions
            {
                ClientId = "fixture-client-id",
                ClientSecret = "fixture-client-secret",
            }),
            TimeProvider.System,
            NullLogger<ChzzkClient>.Instance);
    }

    private static HttpResponseMessage Response(string fixtureName)
    {
        return JsonResponse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Chzzk", fixtureName)));
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
