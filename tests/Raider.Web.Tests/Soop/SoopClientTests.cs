// SOOP 공식 API client의 변환, 전체 페이지, 무쿠키, 오류 계약을 검증한다.
using System.Collections.Immutable;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Raider.Web.Collection;
using Raider.Web.Live;
using Raider.Web.Soop;

namespace Raider.Web.Tests.Soop;

public sealed class SoopClientTests
{
    [Fact]
    public async Task NormalFixtureMapsOfficialViewCountCategoryAndUrls()
    {
        var client = CreateClient(new FixtureHandler(_ => Response("normal.json")));

        var result = await client.CollectAsync(CancellationToken.None);

        var stream = Assert.Single(result);
        Assert.Equal(Platform.Soop, stream.Platform);
        Assert.Equal("2001", stream.BroadcastId);
        Assert.Equal(321, stream.ViewerCount);
        Assert.Equal("https://example.invalid/soop/thumbnail-1.jpg", stream.ThumbnailUrl);
        Assert.Equal("https://play.sooplive.com/fixture-user-1/2001", stream.WatchUrl);
        Assert.Equal(
            ["Fixture category"],
            stream.Tags.ToArray());
    }

    [Fact]
    public async Task MissingOptionalThumbnailIsAllowedAndMissingRequiredStreamIsExcluded()
    {
        var optional = CreateClient(new FixtureHandler(_ => JsonResponse(
            """
            {"total_cnt":1,"page_no":"1","broad":[{"broad_no":"2004","user_id":"user","user_nick":"Streamer","broad_title":"Valid","broad_thumb":null,"total_view_cnt":"1","broad_cate_no":"00130000"}],"time":0}
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
                    {"total_cnt":61,"page_no":"2","broad":[{"broad_no":"2001","user_id":"fixture-user-1","user_nick":"Duplicate","broad_title":"Duplicate","total_view_cnt":"1","broad_cate_no":"00130000"},{"broad_no":"2002","user_id":"fixture-user-2","user_nick":"Fixture streamer two","broad_title":"Fixture live two","total_view_cnt":"123","broad_cate_no":"00130000"}],"time":0}
                    """);
        });

        var result = await CreateClient(handler).CollectAsync(CancellationToken.None);

        Assert.Equal(2, result.Length);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request =>
        {
            Assert.Contains("order_type=broad_start", request.RequestUri!.Query, StringComparison.Ordinal);
            Assert.Contains("client_id=fixture-client-id", request.RequestUri!.Query, StringComparison.Ordinal);
            Assert.False(request.Headers.Contains("Cookie"));
        });
        Assert.Contains("page_no=1", requests[0].RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("page_no=2", requests[1].RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishesFirstPageAndFetchesRemainingPagesSequentially()
    {
        var concurrent = 0;
        var maximumConcurrent = 0;
        var handler = new AsyncFixtureHandler(async (request, cancellationToken) =>
        {
            var current = Interlocked.Increment(ref concurrent);
            UpdateMaximum(ref maximumConcurrent, current);
            try
            {
                await Task.Delay(20, cancellationToken);
                var pageNumber = int.Parse(
                    System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["page_no"]!,
                    System.Globalization.CultureInfo.InvariantCulture);
                return JsonResponse(
                    $$"""
                    {"total_cnt":240,"page_no":"{{pageNumber}}","broad":[{"broad_no":"{{pageNumber}}","user_id":"user-{{pageNumber}}","user_nick":"Streamer {{pageNumber}}","broad_title":"Live {{pageNumber}}","total_view_cnt":"{{pageNumber}}","broad_cate_no":"00130000"}]}
                    """);
            }
            finally
            {
                Interlocked.Decrement(ref concurrent);
            }
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
        Assert.Equal(4, result.Length);
        Assert.Equal(1, maximumConcurrent);
    }

    [Fact]
    public async Task RetriesTransientFailureForAnIndividualPage()
    {
        var requests = 0;
        var handler = new FixtureHandler(_ =>
        {
            requests++;
            if (requests == 1)
            {
                return Response("pagination-first.json");
            }

            if (requests == 2)
            {
                throw new HttpRequestException("Temporary network failure.");
            }

            return JsonResponse(
                """
                {"total_cnt":61,"page_no":"2","broad":[{"broad_no":"2002","user_id":"fixture-user-2","user_nick":"Fixture streamer two","broad_title":"Fixture live two","total_view_cnt":"123","broad_cate_no":"00130000"}]}
                """);
        });

        var result = await CreateClient(handler).CollectAsync(CancellationToken.None);

        Assert.Equal(2, result.Length);
        Assert.Equal(3, requests);
    }

    private static void UpdateMaximum(ref int target, int value)
    {
        int current;
        do
        {
            current = Volatile.Read(ref target);
            if (value <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref target, value, current) != current);
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

    [Fact]
    public async Task MapsInvalidOfficialClientResultAsAuthentication()
    {
        var client = CreateClient(new FixtureHandler(_ => JsonResponse(
            """
            {"result":-1104,"msg":"invalid client"}
            """)));

        var error = await Assert.ThrowsAsync<PlatformCollectionException>(
            () => client.CollectAsync(CancellationToken.None));

        Assert.Equal(PlatformErrorKind.Authentication, error.Error.Kind);
    }

    [Fact]
    public async Task MissingCredentialsFailCollectionAsConfigurationWithoutSendingRequest()
    {
        var requested = false;
        var handler = new FixtureHandler(_ =>
        {
            requested = true;
            return JsonResponse("""{"total_cnt":0,"page_no":"1","broad":[],"time":0}""");
        });

        var error = await Assert.ThrowsAsync<PlatformCollectionException>(
            () => CreateClient(handler, new Raider.Web.Configuration.SoopOptions()).CollectAsync(CancellationToken.None));

        Assert.Equal(PlatformErrorKind.Configuration, error.Error.Kind);
        Assert.False(requested);
    }

    private static SoopClient CreateClient(
        HttpMessageHandler handler,
        Raider.Web.Configuration.SoopOptions? options = null)
    {
        return new SoopClient(
            new HttpClient(new CategoryFixtureHandler(handler))
            {
                BaseAddress = new Uri("https://openapi.sooplive.com/"),
                Timeout = TimeSpan.FromSeconds(5),
            },
            Microsoft.Extensions.Options.Options.Create(options ?? new Raider.Web.Configuration.SoopOptions
            {
                ClientId = "fixture-client-id",
            }),
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

    private sealed class CategoryFixtureHandler(HttpMessageHandler inner) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker invoker = new(inner);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/broad/category/list", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(JsonResponse(
                    """
                    {"broad_category":[{"cate_no":"00130000","cate_name":"Fixture category","child":[]}]}
                    """));
            }

            return invoker.SendAsync(request, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                invoker.Dispose();
            }

            base.Dispose(disposing);
        }
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
