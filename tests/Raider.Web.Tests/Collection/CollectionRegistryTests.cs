using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using Raider.Web.Collection;
using Raider.Web.Live;

namespace Raider.Web.Tests.Collection;

public sealed class CollectionRegistryTests
{
    [Fact]
    public void RegistersWorkersAndChecksIfAnyAreCollecting()
    {
        var registry = new CollectionRegistry();
        var source = new FakeSource();
        var store = new SnapshotStore([Platform.Chzzk]);
        var options = new CollectionOptions
        {
            PollInterval = TimeSpan.FromMinutes(10),
            CollectionTimeout = TimeSpan.FromSeconds(5)
        };

        var worker = new PlatformCollectorWorker(
            source,
            store,
            options,
            registry,
            TimeProvider.System,
            NullLogger<PlatformCollectorWorker>.Instance);

        Assert.False(registry.IsAnyCollecting);
        Assert.False(worker.IsCollecting);
    }

    [Fact]
    public async Task TriggerStartsCollectionBeforeReturning()
    {
        var registry = new CollectionRegistry();
        var source = new BlockingSource();
        _ = new PlatformCollectorWorker(
            source,
            new SnapshotStore([Platform.Chzzk]),
            new CollectionOptions(),
            registry,
            TimeProvider.System,
            NullLogger<PlatformCollectorWorker>.Instance);

        registry.TriggerCollectAll();

        Assert.True(registry.IsAnyCollecting);
        Assert.True(source.Started.Task.IsCompleted);
        source.Complete.SetResult();
        await source.Finished.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    private sealed class FakeSource : ILiveSource
    {
        public Platform Platform => Platform.Chzzk;
        public Task<ImmutableArray<LiveStream>> CollectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ImmutableArray<LiveStream>.Empty);
        }
    }

    private sealed class BlockingSource : ILiveSource
    {
        public Platform Platform => Platform.Chzzk;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ImmutableArray<LiveStream>> CollectAsync(CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Complete.Task.WaitAsync(cancellationToken);
            Finished.SetResult();
            return [];
        }
    }
}
