// 플랫폼 수집 워커의 즉시 실행, 제한 재시도, 중복 방지, 정상 종료를 검증한다.
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Raider.Web.Collection;
using Raider.Web.Live;

namespace Raider.Web.Tests.Collection;

public sealed class PlatformCollectorWorkerTests
{
    [Fact]
    public async Task RetriesOneRetryableFailureAndPublishesSuccess()
    {
        var source = new FakeSource(
            Platform.Chzzk,
            [
                new PlatformCollectionException(new PlatformError(PlatformErrorKind.Server), "server"),
                ImmutableArray.Create(Stream("success", Platform.Chzzk)),
            ]);
        var store = new SnapshotStore([Platform.Chzzk, Platform.Soop]);
        var worker = Worker(source, store);

        await worker.CollectOnceAsync(CancellationToken.None);

        Assert.Equal(2, source.CallCount);
        Assert.Equal("success", Assert.Single(store.Current.Live.Streams).BroadcastId);
    }

    [Fact]
    public async Task DoesNotRetryPermanentFailureAndPreventsOverlappingRuns()
    {
        var source = new FakeSource(
            Platform.Soop,
            [new PlatformCollectionException(new PlatformError(PlatformErrorKind.Contract), "contract")],
            TimeSpan.FromMilliseconds(30));
        var store = new SnapshotStore([Platform.Chzzk, Platform.Soop]);
        var worker = Worker(source, store);

        await Task.WhenAll(worker.CollectOnceAsync(CancellationToken.None), worker.CollectOnceAsync(CancellationToken.None));

        Assert.Equal(1, source.CallCount);
        Assert.Equal(1, source.MaximumConcurrentCalls);
        Assert.Equal(PlatformErrorKind.Contract, store.Current.Platforms[Platform.Soop].Error?.Kind);
    }

    [Fact]
    public async Task BackgroundWorkerRunsImmediatelyPeriodicallyAndStops()
    {
        var source = new FakeSource(Platform.Chzzk, [ImmutableArray<LiveStream>.Empty]);
        var worker = Worker(source, new SnapshotStore([Platform.Chzzk, Platform.Soop]), TimeSpan.FromMilliseconds(20));

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(75);
        await worker.StopAsync(CancellationToken.None);
        var callsAtStop = source.CallCount;
        await Task.Delay(40);

        Assert.True(callsAtStop >= 2);
        Assert.Equal(callsAtStop, source.CallCount);
    }

    [Fact]
    public async Task LogsSafePlatformSuccessAndFinalFailure()
    {
        var logger = new ListLogger<PlatformCollectorWorker>();
        var store = new SnapshotStore([Platform.Chzzk, Platform.Soop]);
        var success = Worker(new FakeSource(Platform.Chzzk, [ImmutableArray.Create(Stream("secret-title", Platform.Chzzk))]), store, logger: logger);
        var failure = Worker(
            new FakeSource(Platform.Soop, [new PlatformCollectionException(new PlatformError(PlatformErrorKind.Contract), "private response")]),
            store,
            logger: logger);

        await success.CollectOnceAsync(CancellationToken.None);
        await failure.CollectOnceAsync(CancellationToken.None);

        Assert.Contains(logger.Messages, message => message.Contains("Platform: Chzzk", StringComparison.Ordinal)
            && message.Contains("Result: Success", StringComparison.Ordinal)
            && message.Contains("StreamCount: 1", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("Platform: Soop", StringComparison.Ordinal)
            && message.Contains("Result: Failure", StringComparison.Ordinal)
            && message.Contains("ErrorKind: Contract", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("secret-title", StringComparison.Ordinal)
            || message.Contains("private response", StringComparison.Ordinal));
    }

    private static PlatformCollectorWorker Worker(
        ILiveSource source,
        SnapshotStore store,
        TimeSpan? pollInterval = null,
        ILogger<PlatformCollectorWorker>? logger = null)
    {
        return new PlatformCollectorWorker(
            source,
            store,
            new CollectionOptions
            {
                PollInterval = pollInterval ?? TimeSpan.FromMinutes(10),
                CollectionTimeout = TimeSpan.FromSeconds(1),
                RetryMinimumDelay = TimeSpan.Zero,
                RetryMaximumDelay = TimeSpan.Zero,
            },
            TimeProvider.System,
            logger ?? NullLogger<PlatformCollectorWorker>.Instance);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private static LiveStream Stream(string id, Platform platform)
    {
        return LiveStream.Create(
            platform,
            id,
            $"channel-{id}",
            $"streamer-{id}",
            $"title-{id}",
            1,
            null,
            $"https://example.invalid/{id}",
            [],
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeSource(
        Platform platform,
        IEnumerable<object> results,
        TimeSpan? delay = null) : ILiveSource
    {
        private readonly Queue<object> results = new(results);
        private int concurrentCalls;

        public Platform Platform { get; } = platform;

        public int CallCount { get; private set; }

        public int MaximumConcurrentCalls { get; private set; }

        public async Task<ImmutableArray<LiveStream>> CollectAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            var concurrent = Interlocked.Increment(ref concurrentCalls);
            MaximumConcurrentCalls = Math.Max(MaximumConcurrentCalls, concurrent);

            try
            {
                if (delay is not null)
                {
                    await Task.Delay(delay.Value, cancellationToken);
                }

                var result = results.Count > 1 ? results.Dequeue() : results.Peek();
                if (result is Exception exception)
                {
                    throw exception;
                }

                return (ImmutableArray<LiveStream>)result;
            }
            finally
            {
                Interlocked.Decrement(ref concurrentCalls);
            }
        }
    }
}
