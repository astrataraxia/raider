// 한 플랫폼을 독립적으로 주기 수집하고 성공 또는 오류 상태를 게시한다.
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Raider.Web.Live;

namespace Raider.Web.Collection;

public sealed class PlatformCollectorWorker : BackgroundService
{
    private readonly ILiveSource source;
    private readonly SnapshotStore snapshots;
    private readonly CollectionOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<PlatformCollectorWorker> logger;
    private readonly SemaphoreSlim execution = new(1, 1);

    public PlatformCollectorWorker(
        ILiveSource source,
        SnapshotStore snapshots,
        CollectionOptions options,
        CollectionRegistry registry,
        TimeProvider timeProvider,
        ILogger<PlatformCollectorWorker> logger)
    {
        this.source = source;
        this.snapshots = snapshots;
        this.options = options;
        this.timeProvider = timeProvider;
        this.logger = logger;

        registry.Register(this);
    }

    public bool IsCollecting => execution.CurrentCount == 0;

    public async Task CollectOnceAsync(CancellationToken cancellationToken)
    {
        if (!await execution.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var started = Stopwatch.GetTimestamp();
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(options.CollectionTimeout);
                    var streams = source is IProgressiveLiveSource progressive
                        ? await progressive.CollectAsync(PublishPartialAsync, timeout.Token)
                        : await source.CollectAsync(timeout.Token);
                    snapshots.ApplySuccess(
                        source.Platform,
                        streams,
                        timeProvider.GetUtcNow(),
                        Stopwatch.GetElapsedTime(started));
                    logger.LogInformation(
                        "Platform collection completed. Platform: {Platform}, Result: {Result}, StreamCount: {StreamCount}, DurationMs: {DurationMs}",
                        source.Platform,
                        "Success",
                        streams.Length,
                        Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    return;
                }
                catch (PlatformCollectionException exception) when (
                    attempt == 0 &&
                    exception.Error.CanRetryImmediately &&
                    !cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning(
                        "Retrying platform collection. Platform: {Platform}, Operation: {Operation}, ErrorKind: {ErrorKind}, RetryAttempt: {RetryAttempt}",
                        source.Platform,
                        "collect",
                        exception.Error.Kind,
                        attempt + 1);
                    await DelayBeforeRetryAsync(cancellationToken);
                }
                catch (PlatformCollectionException exception)
                {
                    snapshots.ApplyFailure(
                        source.Platform,
                        exception.Error,
                        timeProvider.GetUtcNow(),
                        Stopwatch.GetElapsedTime(started));
                    LogFailure(exception.Error, started);
                    return;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    var error = new PlatformError(PlatformErrorKind.Timeout);
                    snapshots.ApplyFailure(
                        source.Platform,
                        error,
                        timeProvider.GetUtcNow(),
                        Stopwatch.GetElapsedTime(started));
                    LogFailure(error, started);
                    return;
                }
            }
        }
        finally
        {
            execution.Release();
        }
    }

    private ValueTask PublishPartialAsync(ImmutableArray<LiveStream> streams)
    {
        snapshots.ApplyPartial(source.Platform, streams, timeProvider.GetUtcNow());
        return ValueTask.CompletedTask;
    }

    private void LogFailure(PlatformError error, long started)
    {
        logger.LogWarning(
            "Platform collection completed. Platform: {Platform}, Result: {Result}, ErrorKind: {ErrorKind}, DurationMs: {DurationMs}",
            source.Platform,
            "Failure",
            error.Kind,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await CollectOnceAsync(stoppingToken);
            await Task.Delay(options.PollInterval, timeProvider, stoppingToken);
        }
    }

    private Task DelayBeforeRetryAsync(CancellationToken cancellationToken)
    {
        if (options.RetryMaximumDelay <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        var minimum = Math.Max(0, (int)options.RetryMinimumDelay.TotalMilliseconds);
        var maximum = Math.Max(minimum + 1, (int)options.RetryMaximumDelay.TotalMilliseconds + 1);
        var delay = TimeSpan.FromMilliseconds(Random.Shared.Next(minimum, maximum));
        return Task.Delay(delay, timeProvider, cancellationToken);
    }
}
