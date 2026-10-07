// 서버 호스트 SQLite 파일에 공용 즐겨찾기를 저장한다.
using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Raider.Web.Live;

namespace Raider.Web.Favorites;

public sealed class FavoriteStore
{
    private const int MaximumAttempts = 3;
    private readonly FavoriteDbContext dbContext;

    public FavoriteStore(string databasePath)
    {
        dbContext = new FavoriteDbContext(databasePath);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);

                // Migration: add category column if it doesn't exist (for existing databases)
                try
                {
                    var connection = dbContext.Database.GetDbConnection();
                    await connection.OpenAsync(cancellationToken);
                    await using var command = connection.CreateCommand();
                    command.CommandText = "ALTER TABLE favorites ADD COLUMN category TEXT NOT NULL DEFAULT '기본';";
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (Microsoft.Data.Sqlite.SqliteException)
                {
                    // Column already exists, ignore
                }

                return;
            }
            catch (Microsoft.Data.Sqlite.SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    public async Task<ImmutableArray<Favorite>> ListAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var favorites = await dbContext.Favorites
                    .OrderBy(f => f.StreamerName)
                    .ThenBy(f => f.Platform)
                    .ThenBy(f => f.ChannelId)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                return favorites.ToImmutableArray();
            }
            catch (Microsoft.Data.Sqlite.SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    public async Task UpsertAsync(Favorite favorite, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(favorite);
        Validate(favorite.Platform, favorite.ChannelId);
        if (string.IsNullOrWhiteSpace(favorite.StreamerName))
        {
            throw new ArgumentException("A streamer name is required.", nameof(favorite));
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var existing = await dbContext.Favorites.FindAsync(
                    new object[] { favorite.Platform, favorite.ChannelId },
                    cancellationToken);

                if (existing is not null)
                {
                    existing.StreamerName = favorite.StreamerName;
                    existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
                }
                else
                {
                    favorite.CreatedAtUtc = DateTimeOffset.UtcNow;
                    favorite.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    dbContext.Favorites.Add(favorite);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (Microsoft.Data.Sqlite.SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    public async Task DeleteAsync(Platform platform, string channelId, CancellationToken cancellationToken)
    {
        Validate(platform, channelId);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var favorite = await dbContext.Favorites.FindAsync(
                    new object[] { platform, channelId },
                    cancellationToken);

                if (favorite is not null)
                {
                    dbContext.Favorites.Remove(favorite);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                return;
            }
            catch (Microsoft.Data.Sqlite.SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    public async Task UpdateCategoryAsync(Platform platform, string channelId, string category, CancellationToken cancellationToken)
    {
        Validate(platform, channelId);
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("A category is required.", nameof(category));
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var favorite = await dbContext.Favorites.FindAsync(
                    new object[] { platform, channelId },
                    cancellationToken);

                if (favorite is not null)
                {
                    favorite.Category = category.Trim();
                    favorite.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                return;
            }
            catch (Microsoft.Data.Sqlite.SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
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
