// SQLite 공용 즐겨찾기 저장소의 영속성과 멱등 동작을 검증한다.
using Raider.Web.Favorites;
using Raider.Web.Live;

namespace Raider.Web.Tests.Favorites;

public sealed class FavoriteStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"raider-favorites-{Guid.NewGuid():N}");

    [Fact]
    public async Task ReopenedStoreKeepsFavoritesAndUpsertDoesNotDuplicate()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "raider.db");
        var first = new FavoriteStore(path);
        await first.InitializeAsync(CancellationToken.None);

        await first.UpsertAsync(new Favorite(Platform.Chzzk, "channel-1", "Alpha"), CancellationToken.None);
        await first.UpsertAsync(new Favorite(Platform.Chzzk, "channel-1", "Alpha"), CancellationToken.None);

        var reopened = new FavoriteStore(path);
        await reopened.InitializeAsync(CancellationToken.None);
        var favorites = await reopened.ListAsync(CancellationToken.None);

        var favorite = Assert.Single(favorites);
        Assert.Equal(Platform.Chzzk, favorite.Platform);
        Assert.Equal("channel-1", favorite.ChannelId);
        Assert.Equal("Alpha", favorite.StreamerName);
    }

    [Fact]
    public async Task DeleteRemovesOnlyMatchingPlatformAndChannel()
    {
        Directory.CreateDirectory(directory);
        var store = new FavoriteStore(Path.Combine(directory, "raider.db"));
        await store.InitializeAsync(CancellationToken.None);
        await store.UpsertAsync(new Favorite(Platform.Chzzk, "same", "Chzzk"), CancellationToken.None);
        await store.UpsertAsync(new Favorite(Platform.Soop, "same", "Soop"), CancellationToken.None);

        await store.DeleteAsync(Platform.Chzzk, "same", CancellationToken.None);

        var favorite = Assert.Single(await store.ListAsync(CancellationToken.None));
        Assert.Equal(Platform.Soop, favorite.Platform);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
