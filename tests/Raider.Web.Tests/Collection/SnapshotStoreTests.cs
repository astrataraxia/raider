// 플랫폼별 결과를 병합한 스냅샷의 원자 교체와 실패 격리를 검증한다.
using System.Collections.Immutable;
using Raider.Web.Collection;
using Raider.Web.Live;

namespace Raider.Web.Tests.Collection;

public sealed class SnapshotStoreTests
{
    [Fact]
    public void StartsEmptyAndReplacesOnlyWithNewerCompletedSnapshots()
    {
        var store = new SnapshotStore([Platform.Chzzk, Platform.Soop]);
        var newer = At(2);
        var older = At(1);

        Assert.Empty(store.Current.Live.Streams);
        Assert.False(store.Current.IsReady);
        Assert.True(store.ApplySuccess(Platform.Chzzk, [Stream("new", Platform.Chzzk)], newer));
        Assert.False(store.ApplySuccess(Platform.Chzzk, [Stream("old", Platform.Chzzk)], older));

        Assert.Equal("new", Assert.Single(store.Current.Live.Streams).BroadcastId);
    }

    [Fact]
    public void FailureKeepsLastSuccessAndOtherPlatformCanUpdate()
    {
        var store = new SnapshotStore([Platform.Chzzk, Platform.Soop]);
        store.ApplySuccess(Platform.Chzzk, [Stream("chzzk", Platform.Chzzk)], At(1));

        store.ApplyFailure(Platform.Chzzk, new PlatformError(PlatformErrorKind.Server), At(2));
        store.ApplySuccess(Platform.Soop, [Stream("soop", Platform.Soop)], At(3));

        Assert.Equal(["chzzk", "soop"], store.Current.Live.Streams.Select(stream => stream.BroadcastId).Order().ToArray());
        Assert.Equal(PlatformErrorKind.Server, store.Current.Platforms[Platform.Chzzk].Error?.Kind);
        Assert.True(store.Current.IsReady);
    }

    [Fact]
    public async Task ConcurrentReadsObserveCompleteSnapshots()
    {
        var store = new SnapshotStore([Platform.Chzzk, Platform.Soop]);
        var reads = Task.Run(() =>
        {
            for (var index = 0; index < 2_000; index++)
            {
                var current = store.Current;
                Assert.Equal(current.Live.Streams.Length, current.Live.Streams.Select(stream => stream.BroadcastId).Distinct().Count());
            }
        });

        for (var index = 1; index <= 100; index++)
        {
            store.ApplySuccess(Platform.Chzzk, [Stream($"stream-{index}", Platform.Chzzk)], At(index));
        }

        await reads;
    }

    private static DateTimeOffset At(int second) =>
        new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero).AddSeconds(second);

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
            At(0));
    }
}
