// CHZZK 채팅 날짜 건수와 방송일 저장 계약을 검증한다.
using Raider.Web.Recap;

namespace Raider.Web.Tests.Recap;

public sealed class ChatCountStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"raider-chat-{Guid.NewGuid():N}");

    [Fact]
    public async Task SameSenderAndDayAddsToCountAndSurvivesReopen()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "raider.db");
        var store = new ChatCountStore(path);
        await store.InitializeAsync(CancellationToken.None);
        var day = new DateOnly(2026, 9, 18);

        await store.AddChatAsync("home", "me", day, CancellationToken.None);
        await store.AddChatAsync("home", "me", day, CancellationToken.None);

        var reopened = new ChatCountStore(path);
        await reopened.InitializeAsync(CancellationToken.None);
        var counts = await reopened.ListViewerDaysAsync("me", CancellationToken.None);

        var row = Assert.Single(counts);
        Assert.Equal("home", row.ChannelId);
        Assert.Equal("me", row.SenderChannelId);
        Assert.Equal(day, row.Date);
        Assert.Equal(2, row.Count);
    }

    [Fact]
    public async Task DifferentDaysAndSendersStaySeparate()
    {
        Directory.CreateDirectory(directory);
        var store = new ChatCountStore(Path.Combine(directory, "raider.db"));
        await store.InitializeAsync(CancellationToken.None);
        var monday = new DateOnly(2026, 9, 14);
        var tuesday = new DateOnly(2026, 9, 15);

        await store.AddChatAsync("home", "me", monday, CancellationToken.None);
        await store.AddChatAsync("home", "other", monday, CancellationToken.None);
        await store.AddChatAsync("home", "me", tuesday, CancellationToken.None);

        var mine = await store.ListViewerDaysAsync("me", CancellationToken.None);

        Assert.Equal(2, mine.Length);
        Assert.Equal(1, mine.Single(row => row.Date == monday).Count);
        Assert.Equal(1, mine.Single(row => row.Date == tuesday).Count);
    }

    [Fact]
    public async Task BroadcastDayIsRecordedOnceAndListed()
    {
        Directory.CreateDirectory(directory);
        var store = new ChatCountStore(Path.Combine(directory, "raider.db"));
        await store.InitializeAsync(CancellationToken.None);
        var day = new DateOnly(2026, 9, 18);

        await store.RecordBroadcastDayAsync("home", day, CancellationToken.None);
        await store.RecordBroadcastDayAsync("home", day, CancellationToken.None);

        var days = await store.ListBroadcastDaysAsync("home", CancellationToken.None);

        Assert.Equal(day, Assert.Single(days));
    }

    [Fact]
    public async Task FirstSeenDatesAreEarliestChatDayPerSender()
    {
        Directory.CreateDirectory(directory);
        var store = new ChatCountStore(Path.Combine(directory, "raider.db"));
        await store.InitializeAsync(CancellationToken.None);

        await store.AddChatAsync("home", "early", new DateOnly(2026, 6, 1), CancellationToken.None);
        await store.AddChatAsync("home", "me", new DateOnly(2026, 6, 2), CancellationToken.None);
        await store.AddChatAsync("home", "early", new DateOnly(2026, 6, 10), CancellationToken.None);

        var firstSeen = await store.ListFirstSeenAsync("home", CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 6, 1), firstSeen.Single(row => row.SenderChannelId == "early").FirstSeen);
        Assert.Equal(new DateOnly(2026, 6, 2), firstSeen.Single(row => row.SenderChannelId == "me").FirstSeen);
    }

    [Fact]
    public async Task ExistingFavoritesDatabaseGainsChatTables()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "raider.db");
        var favorites = new Raider.Web.Favorites.FavoriteStore(path);
        await favorites.InitializeAsync(CancellationToken.None);

        var store = new ChatCountStore(path);
        await store.InitializeAsync(CancellationToken.None);
        await store.AddChatAsync("home", "me", new DateOnly(2026, 9, 18), CancellationToken.None);

        Assert.Single(await store.ListViewerDaysAsync("me", CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
