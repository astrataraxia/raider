// SOOP 공식 API의 전체 현재 라이브 목록을 수집하고 공통 모델로 변환한다.
using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raider.Web.Collection;
using Raider.Web.Configuration;
using Raider.Web.Live;

namespace Raider.Web.Soop;

public sealed class SoopClient : IProgressiveLiveSource
{
    private const int PageSize = 60;
    private const int MaximumPages = 100;
    private const int MaximumConcurrentRequests = 1;
    private const int MaximumRequestAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);
    private readonly HttpClient httpClient;
    private readonly SoopOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SoopClient> logger;

    public SoopClient(
        HttpClient httpClient,
        IOptions<SoopOptions> options,
        TimeProvider timeProvider,
        ILogger<SoopClient> logger)
    {
        this.httpClient = httpClient;
        if (this.httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Raider/0.1");
        }

        this.options = options.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public Platform Platform => Platform.Soop;

    public Task<ImmutableArray<LiveStream>> CollectAsync(CancellationToken cancellationToken)
    {
        return CollectCoreAsync(null, cancellationToken);
    }

    public Task<ImmutableArray<LiveStream>> CollectAsync(
        Func<ImmutableArray<LiveStream>, ValueTask> publishPartial,
        CancellationToken cancellationToken)
    {
        return CollectCoreAsync(publishPartial, cancellationToken);
    }

    private async Task<ImmutableArray<LiveStream>> CollectCoreAsync(
        Func<ImmutableArray<LiveStream>, ValueTask>? publishPartial,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var streams = new List<LiveStream>();
        var firstPage = await GetPageWithRetryAsync(1, cancellationToken);
        var pageCount = (int)Math.Ceiling(firstPage.TotalCount / (double)PageSize);
        if (pageCount > MaximumPages)
        {
            throw ContractError();
        }

        var categoryNames = await GetCategoryNamesAsync(cancellationToken);
        var excludedCount = Map(firstPage.Broadcasts, categoryNames, streams);
        if (publishPartial is not null && streams.Count > 0 && pageCount > 1)
        {
            await publishPartial(LiveStream.OrderAndDeduplicate(streams));
        }

        excludedCount += await FetchRemainingPagesAsync(pageCount, categoryNames, streams, cancellationToken);

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

    private async Task<int> FetchRemainingPagesAsync(
        int pageCount,
        IReadOnlyDictionary<string, string> categoryNames,
        List<LiveStream> streams,
        CancellationToken cancellationToken)
    {
        if (pageCount <= 1)
        {
            return 0;
        }

        var excludedCount = 0;
        await Parallel.ForEachAsync(
            Enumerable.Range(2, pageCount - 1),
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = MaximumConcurrentRequests,
            },
            async (pageNumber, token) =>
            {
                var pageStreams = new List<LiveStream>(PageSize);
                var page = await GetPageWithRetryAsync(pageNumber, token);
                Interlocked.Add(ref excludedCount, Map(page.Broadcasts, categoryNames, pageStreams));
                lock (streams)
                {
                    streams.AddRange(pageStreams);
                }
            });
        return excludedCount;
    }

    private void EnsureConfigured()
    {
        if (!options.IsConfigured)
        {
            throw new PlatformCollectionException(
                new PlatformError(PlatformErrorKind.Configuration),
                "SOOP ClientId is required.");
        }
    }

    private async Task<SoopPage> GetPageWithRetryAsync(int pageNumber, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await GetPageAsync(pageNumber, cancellationToken);
            }
            catch (PlatformCollectionException exception) when (
                attempt < MaximumRequestAttempts &&
                exception.Error.CanRetryImmediately &&
                !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    exception,
                    "Retrying SOOP page collection. Platform: {Platform}, PageNumber: {PageNumber}, ErrorKind: {ErrorKind}, RetryAttempt: {RetryAttempt}",
                    Platform.Soop,
                    pageNumber,
                    exception.Error.Kind,
                    attempt);
                await Task.Delay(RetryDelay, timeProvider, cancellationToken);
            }
        }
    }

    private int Map(
        IEnumerable<SoopBroadcast> broadcasts,
        IReadOnlyDictionary<string, string> categoryNames,
        List<LiveStream> streams)
    {
        var excludedCount = 0;
        foreach (var broadcast in broadcasts)
        {
            if (TryMap(broadcast, categoryNames, out var stream))
            {
                streams.Add(stream);
            }
            else
            {
                excludedCount++;
            }
        }

        return excludedCount;
    }

    private async Task<SoopPage> GetPageAsync(int pageNumber, CancellationToken cancellationToken)
    {
        var path = $"broad/list?client_id={Uri.EscapeDataString(options.ClientId)}&select_key=cate&order_type=broad_start&page_no={pageNumber}";

        try
        {
            using var response = await httpClient.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpError(response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<SoopResponse>(cancellationToken: cancellationToken);
            if (result?.Result is < 0)
            {
                throw ApiResultError(result.Result.Value);
            }

            if (result?.TotalCount is null ||
                string.IsNullOrWhiteSpace(result.PageNumber) ||
                result.Broadcasts is null ||
                result.TotalCount < 0 ||
                !int.TryParse(result.PageNumber, NumberStyles.None, CultureInfo.InvariantCulture, out var responsePageNumber) ||
                responsePageNumber != pageNumber)
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

    private async Task<IReadOnlyDictionary<string, string>> GetCategoryNamesAsync(CancellationToken cancellationToken)
    {
        var path = $"broad/category/list?client_id={Uri.EscapeDataString(options.ClientId)}&locale=ko_KR";

        try
        {
            using var response = await httpClient.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpError(response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<SoopCategoryResponse>(cancellationToken: cancellationToken);
            if (result?.Categories is null)
            {
                throw ContractError();
            }

            var categories = new Dictionary<string, string>(StringComparer.Ordinal);
            AddCategoryNames(result.Categories, categories);
            return categories;
        }
        catch (PlatformCollectionException exception)
        {
            logger.LogWarning(
                exception,
                "SOOP category metadata unavailable. Platform: {Platform}, Operation: {Operation}, ErrorKind: {ErrorKind}",
                Platform.Soop,
                "category-list",
                exception.Error.Kind);
            return ImmutableDictionary<string, string>.Empty;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "SOOP category metadata request timed out.");
            return ImmutableDictionary<string, string>.Empty;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "SOOP category metadata request failed.");
            return ImmutableDictionary<string, string>.Empty;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "SOOP category metadata response was invalid.");
            return ImmutableDictionary<string, string>.Empty;
        }
    }

    private static void AddCategoryNames(IEnumerable<SoopCategory> categories, IDictionary<string, string> names)
    {
        foreach (var category in categories)
        {
            if (!string.IsNullOrWhiteSpace(category.Number) && !string.IsNullOrWhiteSpace(category.Name))
            {
                names[category.Number] = category.Name;
            }

            if (category.Children is not null)
            {
                AddCategoryNames(category.Children, names);
            }
        }
    }

    private bool TryMap(
        SoopBroadcast broadcast,
        IReadOnlyDictionary<string, string> categoryNames,
        out LiveStream stream)
    {
        try
        {
            if (!long.TryParse(broadcast.BroadcastNumber, NumberStyles.None, CultureInfo.InvariantCulture, out var broadcastNumber) ||
                broadcastNumber <= 0 ||
                !int.TryParse(broadcast.TotalViewCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var viewerCount) ||
                viewerCount < 0)
            {
                throw new ArgumentException("SOOP broadcast identifiers and viewer counts must be valid.");
            }

            var tags = !string.IsNullOrWhiteSpace(broadcast.CategoryNumber) &&
                categoryNames.TryGetValue(broadcast.CategoryNumber, out var categoryName)
                ? new[] { categoryName }
                : Array.Empty<string>();

            stream = LiveStream.Create(
                Platform.Soop,
                broadcastNumber.ToString(CultureInfo.InvariantCulture),
                broadcast.UserId ?? string.Empty,
                broadcast.UserNickname ?? string.Empty,
                broadcast.Title ?? string.Empty,
                viewerCount,
                NormalizeThumbnail(broadcast.Thumbnail),
                $"https://play.sooplive.com/{broadcast.UserId}/{broadcastNumber}",
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

    private static PlatformCollectionException ApiResultError(int result)
    {
        var kind = result == -1104 ? PlatformErrorKind.Authentication : PlatformErrorKind.Contract;
        return new PlatformCollectionException(
            new PlatformError(kind),
            $"SOOP API returned result {result}.");
    }

    private static string? NormalizeThumbnail(string? thumbnail)
    {
        return thumbnail?.StartsWith("//", StringComparison.Ordinal) == true ? $"https:{thumbnail}" : thumbnail;
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
