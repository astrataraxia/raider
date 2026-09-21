// 즐겨찾기 CHZZK 라이브만 채팅 집계 대상으로 삼는 계약을 검증한다.
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Raider.Web.Collection;
using Raider.Web.Favorites;
using Raider.Web.Live;
using Raider.Web.Recap;

namespace Raider.Web.Tests.Recap;

public sealed class ChzzkChatWorkerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"raider-chat-worker-{Guid.NewGuid():N}");

    [Fact]
    public async Task ObserveRecordsBroadcastDayOnlyForLiveChzzkFavorites()
    {
        var (worker, store, now) = await CreateWorkerAsync(
            [new Favorite(Platform.Chzzk, "channel-alpha", "Alpha"), new Favorite(Platform.Soop, "channel-soop", "Soop")],
            [
                Stream("alpha", Platform.Chzzk),
                Stream("beta", Platform.Chzzk),
                Stream("soop", Platform.Soop),
            ]);

        await worker.ObserveLiveFavoritesAsync(CancellationToken.None);

        var day = SeoulCalendar.DateFrom(now);
        Assert.Equal(day, Assert.Single(await store.ListBroadcastDaysAsync("channel-alpha", CancellationToken.None)));
        Assert.Empty(await store.ListBroadcastDaysAsync("channel-beta", CancellationToken.None));
        Assert.Empty(await store.ListBroadcastDaysAsync("channel-soop", CancellationToken.None));
    }

    [Fact]
    public async Task IngestAddsSenderCountAndDropsMessageText()
    {
        var (worker, store, _) = await CreateWorkerAsync(
            [new Favorite(Platform.Chzzk, "channel-alpha", "Alpha")],
            [Stream("alpha", Platform.Chzzk)]);

        await worker.IngestFrameAsync(
            "channel-alpha",
            """{"cmd":93101,"bdy":[{"uid":"me","msg":"do-not-store","msgTime":1}]}""",
            CancellationToken.None);

        var row = Assert.Single(await store.ListViewerDaysAsync("me", CancellationToken.None));
        Assert.Equal("channel-alpha", row.ChannelId);
        Assert.Equal(1, row.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private async Task<(ChzzkChatWorker Worker, ChatCountStore Store, DateTimeOffset Now)> CreateWorkerAsync(
        Favorite[] saved,
        LiveStream[] live)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "raider.db");
        var favoriteStore = new FavoriteStore(path);
        await favoriteStore.InitializeAsync(CancellationToken.None);
        foreach (var favorite in saved)
        {
            await favoriteStore.UpsertAsync(favorite, CancellationToken.None);
        }

        var chatStore = new ChatCountStore(path);
        await chatStore.InitializeAsync(CancellationToken.None);
        var snapshots = new SnapshotStore([Platform.Chzzk, Platform.Soop]);
        var now = DateTimeOffset.Parse("2026-09-18T12:00:00+09:00");
        snapshots.ApplySuccess(Platform.Chzzk, [.. live.Where(stream => stream.Platform == Platform.Chzzk)], now);
        snapshots.ApplySuccess(Platform.Soop, [.. live.Where(stream => stream.Platform == Platform.Soop)], now.AddTicks(1));
        var worker = new ChzzkChatWorker(
            chatStore,
            favoriteStore,
            snapshots,
            new ChzzkChatAccess(new HttpClient(new ClosedHandler())),
            TimeProvider.System,
            Options.Create(new ChatOptions { Enabled = false }),
            NullLogger<ChzzkChatWorker>.Instance);
        return (worker, chatStore, DateTimeOffset.UtcNow);
    }

    private static LiveStream Stream(string id, Platform platform)
        => LiveStream.Create(
            platform,
            id,
            $"channel-{id}",
            id,
            "Live",
            1,
            "https://example.invalid/t.jpg",
            $"https://example.invalid/{id}",
            [],
            DateTimeOffset.UtcNow);

    private sealed class ClosedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }
}
