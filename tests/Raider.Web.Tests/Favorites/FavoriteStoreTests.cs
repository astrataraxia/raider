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

    [Fact]
    public async Task UpdateCategoryModifiesCategoryAndKeepsOtherFields()
    {
        Directory.CreateDirectory(directory);
        var store = new FavoriteStore(Path.Combine(directory, "raider.db"));
        await store.InitializeAsync(CancellationToken.None);
        await store.UpsertAsync(new Favorite(Platform.Chzzk, "channel-1", "Alpha", "기본"), CancellationToken.None);

        await store.UpdateCategoryAsync(Platform.Chzzk, "channel-1", "게임", CancellationToken.None);

        var favorites = await store.ListAsync(CancellationToken.None);
        var favorite = Assert.Single(favorites);
        Assert.Equal("게임", favorite.Category);
        Assert.Equal("Alpha", favorite.StreamerName);
    }

    [Fact]
    public async Task RepeatedUpsertPreservesExistingCategory()
    {
        Directory.CreateDirectory(directory);
        var store = new FavoriteStore(Path.Combine(directory, "raider.db"));
        await store.InitializeAsync(CancellationToken.None);
        await store.UpsertAsync(new Favorite(Platform.Chzzk, "channel-1", "Alpha"), CancellationToken.None);
        await store.UpdateCategoryAsync(Platform.Chzzk, "channel-1", "게임", CancellationToken.None);

        await store.UpsertAsync(new Favorite(Platform.Chzzk, "channel-1", "Renamed"), CancellationToken.None);

        var favorite = Assert.Single(await store.ListAsync(CancellationToken.None));
        Assert.Equal("게임", favorite.Category);
        Assert.Equal("Renamed", favorite.StreamerName);
    }

    [Fact]
    public async Task MigrationAddsCategoryColumnToExistingDatabase()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "raider.db");

        // Manually create the old v1.2.0 schema without category column
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=false"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE favorites (
                    platform TEXT NOT NULL,
                    channel_id TEXT NOT NULL,
                    streamer_name TEXT NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL,
                    PRIMARY KEY (platform, channel_id)
                );
                INSERT INTO favorites (platform, channel_id, streamer_name, created_at_utc, updated_at_utc)
                VALUES ('chzzk', 'channel-old', 'Old Streamer', '2026-06-14T00:00:00Z', '2026-06-14T00:00:00Z');
                """;
            await command.ExecuteNonQueryAsync();
        }

        // Initialize with new FavoriteStore (should trigger migration)
        var store = new FavoriteStore(path);
        await store.InitializeAsync(CancellationToken.None);

        // Verify category is readable and defaulted to '기본'
        var favorites = await store.ListAsync(CancellationToken.None);
        var favorite = Assert.Single(favorites);
        Assert.Equal("channel-old", favorite.ChannelId);
        Assert.Equal("Old Streamer", favorite.StreamerName);
        Assert.Equal("기본", favorite.Category);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
