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

    private sealed class FakeSource : ILiveSource
    {
        public Platform Platform => Platform.Chzzk;
        public Task<ImmutableArray<LiveStream>> CollectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ImmutableArray<LiveStream>.Empty);
        }
    }
}
