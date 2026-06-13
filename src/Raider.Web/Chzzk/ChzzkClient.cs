// CHZZK 공식 API의 전체 현재 라이브 목록을 수집하고 공통 모델로 변환한다.
using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raider.Web.Collection;
using Raider.Web.Configuration;
using Raider.Web.Live;

namespace Raider.Web.Chzzk;

public sealed class ChzzkClient : ILiveSource
{
    private readonly HttpClient httpClient;
    private readonly ChzzkOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<ChzzkClient> logger;

    public ChzzkClient(
        HttpClient httpClient,
        IOptions<ChzzkOptions> options,
        TimeProvider timeProvider,
        ILogger<ChzzkClient> logger)
    {
        this.httpClient = httpClient;
        if (this.httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Raider/0.1");
        }

        this.options = options.Value;
        this.options.Validate();
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public Platform Platform => Platform.Chzzk;

    public async Task<ImmutableArray<LiveStream>> CollectAsync(CancellationToken cancellationToken)
    {
        var streams = new List<LiveStream>();
        var excludedCount = 0;
        string? next = null;

        do
        {
            var page = await GetPageAsync(next, cancellationToken);
            foreach (var live in page.Data)
            {
                if (TryMap(live, out var stream))
                {
                    streams.Add(stream);
                }
                else
                {
                    excludedCount++;
                }
            }

            next = page.Page.Next;
        }
        while (!string.IsNullOrWhiteSpace(next));

        if (excludedCount > 0)
        {
            logger.LogWarning(
                "Excluded invalid live streams. Platform: {Platform}, Operation: {Operation}, ErrorKind: {ErrorKind}, ExcludedCount: {ExcludedCount}",
                Platform.Chzzk,
                "collect",
                PlatformErrorKind.Domain,
                excludedCount);
        }

        return LiveStream.OrderAndDeduplicate(streams);
    }

    private async Task<ChzzkContent> GetPageAsync(string? next, CancellationToken cancellationToken)
    {
        var path = "open/v1/lives?size=20";
        if (!string.IsNullOrWhiteSpace(next))
        {
            path += $"&next={Uri.EscapeDataString(next)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Client-Id", options.ClientId);
        request.Headers.Add("Client-Secret", options.ClientSecret);

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpError(response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<ChzzkResponse>(cancellationToken: cancellationToken);
            var content = result?.Content;
            if (result?.Code != 200 || content?.Data is null || content.Page is null)
            {
                throw ContractError();
            }

            return content;
        }
        catch (PlatformCollectionException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PlatformCollectionException(
                new PlatformError(PlatformErrorKind.Timeout),
                "CHZZK request timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new PlatformCollectionException(
                new PlatformError(PlatformErrorKind.Network),
                "CHZZK network request failed.",
                exception);
        }
        catch (JsonException exception)
        {
            throw new PlatformCollectionException(
                new PlatformError(PlatformErrorKind.Contract),
                "CHZZK response contract was invalid.",
                exception);
        }
    }

    private bool TryMap(ChzzkLive live, out LiveStream stream)
    {
        try
        {
            if (live.LiveId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(live.LiveId));
            }

            var tags = live.Tags ?? [];
            if (!string.IsNullOrWhiteSpace(live.LiveCategoryValue))
            {
                tags = [.. tags, live.LiveCategoryValue];
            }

            stream = LiveStream.Create(
                Platform.Chzzk,
                live.LiveId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                live.ChannelId ?? string.Empty,
                live.ChannelName ?? string.Empty,
                live.LiveTitle ?? string.Empty,
                live.ConcurrentUserCount,
                ResolveThumbnailUrl(live.LiveThumbnailImageUrl),
                $"https://chzzk.naver.com/live/{live.ChannelId}",
                tags,
                timeProvider.GetUtcNow());
            return true;
        }
        catch (ArgumentException)
        {
            stream = null!;
            return false;
        }
    }

    private static string? ResolveThumbnailUrl(string? value)
    {
        return value?.Replace("{type}", "480", StringComparison.OrdinalIgnoreCase);
    }

    private static PlatformCollectionException CreateHttpError(HttpStatusCode statusCode)
    {
        var kind = statusCode switch
        {
            HttpStatusCode.Unauthorized => PlatformErrorKind.Authentication,
            HttpStatusCode.Forbidden => PlatformErrorKind.Forbidden,
            HttpStatusCode.RequestTimeout => PlatformErrorKind.Timeout,
            HttpStatusCode.TooManyRequests => PlatformErrorKind.RateLimited,
            >= HttpStatusCode.InternalServerError => PlatformErrorKind.Server,
            _ => PlatformErrorKind.Contract,
        };

        return new PlatformCollectionException(new PlatformError(kind), $"CHZZK request failed with HTTP {(int)statusCode}.");
    }

    private static PlatformCollectionException ContractError()
    {
        return new PlatformCollectionException(
            new PlatformError(PlatformErrorKind.Contract),
            "CHZZK response contract was invalid.");
    }
}
