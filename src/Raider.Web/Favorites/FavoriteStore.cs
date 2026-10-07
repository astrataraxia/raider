// 서버 호스트 SQLite 파일에 공용 즐겨찾기를 저장한다.
using System.Collections.Immutable;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Raider.Web;
using Raider.Web.Live;

namespace Raider.Web.Favorites;

public sealed class FavoriteStore
{
    private readonly IDbContextFactory<FavoriteDbContext> contexts;

    public FavoriteStore(IDbContextFactory<FavoriteDbContext> contexts)
    {
        this.contexts = contexts;
    }

    public FavoriteStore(string databasePath)
        : this(FactoryFor(databasePath))
    {
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
        => SqliteDatabase.ExecuteAsync(contexts, async (db, token) =>
        {
            await db.Database.EnsureCreatedAsync(token);

            // Migration: add category column if it doesn't exist (for existing databases)
            try
            {
                await SqliteDatabase.OpenAsync(db, token);
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "ALTER TABLE favorites ADD COLUMN category TEXT NOT NULL DEFAULT '기본';";
                await command.ExecuteNonQueryAsync(token);
            }
            catch (SqliteException)
            {
                // Column already exists, ignore
            }
        }, cancellationToken);

    public Task<ImmutableArray<Favorite>> ListAsync(CancellationToken cancellationToken)
        => SqliteDatabase.ExecuteAsync<FavoriteDbContext, ImmutableArray<Favorite>>(contexts, async (db, token) =>
        {
            var favorites = await db.Favorites
                .OrderBy(f => f.StreamerName)
                .ThenBy(f => f.Platform)
                .ThenBy(f => f.ChannelId)
                .AsNoTracking()
                .ToListAsync(token);

            return favorites.ToImmutableArray();
        }, cancellationToken);

    public Task UpsertAsync(Favorite favorite, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(favorite);
        Validate(favorite.Platform, favorite.ChannelId);
        if (string.IsNullOrWhiteSpace(favorite.StreamerName))
        {
            throw new ArgumentException("A streamer name is required.", nameof(favorite));
        }

        return SqliteDatabase.ExecuteAsync(contexts, async (db, token) =>
        {
            var existing = await db.Favorites.FindAsync(
                new object[] { favorite.Platform, favorite.ChannelId },
                token);

            if (existing is not null)
            {
                existing.StreamerName = favorite.StreamerName;
                existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }
            else
            {
                favorite.CreatedAtUtc = DateTimeOffset.UtcNow;
                favorite.UpdatedAtUtc = DateTimeOffset.UtcNow;
                db.Favorites.Add(favorite);
            }

            await db.SaveChangesAsync(token);
        }, cancellationToken);
    }

    public Task DeleteAsync(Platform platform, string channelId, CancellationToken cancellationToken)
    {
        Validate(platform, channelId);

        return SqliteDatabase.ExecuteAsync(contexts, async (db, token) =>
        {
            var favorite = await db.Favorites.FindAsync(
                new object[] { platform, channelId },
                token);

            if (favorite is not null)
            {
                db.Favorites.Remove(favorite);
                await db.SaveChangesAsync(token);
            }
        }, cancellationToken);
    }

    public Task UpdateCategoryAsync(Platform platform, string channelId, string category, CancellationToken cancellationToken)
    {
        Validate(platform, channelId);
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("A category is required.", nameof(category));
        }

        return SqliteDatabase.ExecuteAsync(contexts, async (db, token) =>
        {
            var favorite = await db.Favorites.FindAsync(
                new object[] { platform, channelId },
                token);

            if (favorite is not null)
            {
                favorite.Category = category.Trim();
                favorite.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(token);
            }
        }, cancellationToken);
    }

    private static SqliteContextFactory<FavoriteDbContext> FactoryFor(string databasePath)
    {
        var builder = new DbContextOptionsBuilder<FavoriteDbContext>();
        builder.UseRaiderSqlite(databasePath);
        return new SqliteContextFactory<FavoriteDbContext>(() => new FavoriteDbContext(builder.Options));
    }

    private static void Validate(Platform platform, string channelId)
    {
        if (!Enum.IsDefined(platform))
        {
            throw new ArgumentOutOfRangeException(nameof(platform));
        }

        if (string.IsNullOrWhiteSpace(channelId))
        {
            throw new ArgumentException("A channel ID is required.", nameof(channelId));
        }
    }
}
