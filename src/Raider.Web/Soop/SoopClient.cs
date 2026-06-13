// SOOP 공개 웹 JSON의 전체 현재 라이브 목록을 수집하고 공통 모델로 변환한다.
using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Raider.Web.Collection;
using Raider.Web.Live;

namespace Raider.Web.Soop;

public sealed class SoopClient : ILiveSource
{
    private const int PageSize = 60;
    private const int MaximumPages = 100;
    private readonly HttpClient httpClient;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SoopClient> logger;

    public SoopClient(HttpClient httpClient, TimeProvider timeProvider, ILogger<SoopClient> logger)
    {
        this.httpClient = httpClient;
        if (this.httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Raider/0.1");
        }

        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public Platform Platform => Platform.Soop;

    public async Task<ImmutableArray<LiveStream>> CollectAsync(CancellationToken cancellationToken)
    {
        var streams = new List<LiveStream>();
        var excludedCount = 0;
        var pageNumber = 1;
        int? pageCount = null;

        do
        {
            var page = await GetPageAsync(pageNumber, cancellationToken);
            pageCount ??= (int)Math.Ceiling(page.TotalCount / (double)PageSize);
            if (pageCount > MaximumPages)
            {
                throw ContractError();
            }

            foreach (var broadcast in page.Broadcasts)
            {
                if (TryMap(broadcast, out var stream))
                {
                    streams.Add(stream);
                }
                else
                {
                    excludedCount++;
                }
            }

            pageNumber++;
        }
        while (pageNumber <= pageCount);

        if (excludedCount > 0)
        {
            logger.LogWarning(
                "Excluded invalid live streams. Platform: {Platform}, Operation: {Operation}, ErrorKind: {ErrorKind}, ExcludedCount: {ExcludedCount}",
                Platform.Soop,
                "collect",
                PlatformErrorKind.Domain,
                excludedCount);
        }

        return LiveStream.OrderAndDeduplicate(streams);
    }

    private async Task<SoopPage> GetPageAsync(int pageNumber, CancellationToken cancellationToken)
    {
        var path = $"api/main_broad_list_api.php?selectType=action&selectValue=all&orderType=broad_start&pageNo={pageNumber}&lang=ko_KR";

        try
        {
            using var response = await httpClient.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpError(response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<SoopResponse>(cancellationToken: cancellationToken);
            if (result?.TotalCount is null ||
                result.Count is null ||
                result.Broadcasts is null ||
                result.TotalCount < 0 ||
                result.Count < 0)
            {
                throw ContractError();
            }

            return new SoopPage(result.TotalCount.Value, result.Broadcasts);
        }
        catch (PlatformCollectionException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PlatformCollectionException(
                new PlatformError(PlatformErrorKind.Timeout),
                "SOOP request timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new PlatformCollectionException(
                new PlatformError(PlatformErrorKind.Network),
                "SOOP network request failed.",
                exception);
        }
        catch (JsonException exception)
        {
            throw new PlatformCollectionException(
                new PlatformError(PlatformErrorKind.Contract),
                "SOOP response contract was invalid.",
                exception);
        }
    }

    private bool TryMap(SoopBroadcast broadcast, out LiveStream stream)
    {
        try
        {
            if (broadcast.BroadcastNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(broadcast.BroadcastNumber));
            }

            var tags = Enumerable.Empty<string>()
                .Concat(broadcast.AutoHashtags ?? [])
                .Concat(broadcast.CategoryTags ?? [])
                .Concat(broadcast.HashTags ?? [])
                .Concat(broadcast.LanguageTags ?? []);
            if (!string.IsNullOrWhiteSpace(broadcast.CategoryName))
            {
                tags = tags.Append(broadcast.CategoryName);
            }

            stream = LiveStream.Create(
                Platform.Soop,
                broadcast.BroadcastNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                broadcast.UserId ?? string.Empty,
                broadcast.UserNickname ?? string.Empty,
                broadcast.Title ?? string.Empty,
                broadcast.CurrentViewerCount,
                NormalizeThumbnail(broadcast.Thumbnail),
                $"https://play.sooplive.co.kr/{broadcast.UserId}/{broadcast.BroadcastNumber}",
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

    private static string? NormalizeThumbnail(string? thumbnail)
    {
        return thumbnail?.StartsWith("//", StringComparison.Ordinal) == true ? $"https:{thumbnail}" : thumbnail;
    }

    private static PlatformCollectionException CreateHttpError(HttpStatusCode statusCode)
    {
        var kind = statusCode switch
        {
            HttpStatusCode.Forbidden => PlatformErrorKind.Forbidden,
            HttpStatusCode.RequestTimeout => PlatformErrorKind.Timeout,
            HttpStatusCode.TooManyRequests => PlatformErrorKind.RateLimited,
            >= HttpStatusCode.InternalServerError => PlatformErrorKind.Server,
            _ => PlatformErrorKind.Contract,
        };

        return new PlatformCollectionException(new PlatformError(kind), $"SOOP request failed with HTTP {(int)statusCode}.");
    }

    private static PlatformCollectionException ContractError()
    {
        return new PlatformCollectionException(
            new PlatformError(PlatformErrorKind.Contract),
            "SOOP response contract was invalid.");
    }

    private sealed record SoopPage(int TotalCount, SoopBroadcast[] Broadcasts);
}
