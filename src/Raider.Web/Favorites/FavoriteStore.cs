// 서버 호스트 SQLite 파일에 공용 즐겨찾기를 저장한다.
using System.Collections.Immutable;
using Microsoft.Data.Sqlite;
using Raider.Web.Live;

namespace Raider.Web.Favorites;

public sealed class FavoriteStore
{
    private const int MaximumAttempts = 3;
    private readonly string databasePath;
    private readonly string connectionString;

    public FavoriteStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("A database path is required.", nameof(databasePath));
        }

        this.databasePath = databasePath;
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 2,
        }.ToString();
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        return ExecuteAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS favorites (
                        platform TEXT NOT NULL,
                        channel_id TEXT NOT NULL,
                        streamer_name TEXT NOT NULL,
                        category TEXT NOT NULL DEFAULT '기본',
                        created_at_utc TEXT NOT NULL,
                        updated_at_utc TEXT NOT NULL,
                        PRIMARY KEY (platform, channel_id)
                    );
                    """;
                await command.ExecuteNonQueryAsync(cancellationToken);

                try
                {
                    command.CommandText = "ALTER TABLE favorites ADD COLUMN category TEXT NOT NULL DEFAULT '기본';";
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqliteException)
                {
                    // Ignore column already exists exception
                }

                return true;
            },
            cancellationToken);
    }

    public Task<ImmutableArray<Favorite>> ListAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT platform, channel_id, streamer_name, category
                    FROM favorites
                    ORDER BY streamer_name COLLATE NOCASE, platform, channel_id;
                    """;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var favorites = ImmutableArray.CreateBuilder<Favorite>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (TryParsePlatform(reader.GetString(0), out var platform))
                    {
                        favorites.Add(new Favorite(platform, reader.GetString(1), reader.GetString(2), reader.GetString(3)));
                    }
                }

                return favorites.ToImmutable();
            },
            cancellationToken);
    }

    public Task UpsertAsync(Favorite favorite, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(favorite);
        Validate(favorite.Platform, favorite.ChannelId);
        if (string.IsNullOrWhiteSpace(favorite.StreamerName))
        {
            throw new ArgumentException("A streamer name is required.", nameof(favorite));
        }

        return ExecuteAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO favorites (platform, channel_id, streamer_name, category, created_at_utc, updated_at_utc)
                    VALUES ($platform, $channelId, $streamerName, $category, $now, $now)
                    ON CONFLICT(platform, channel_id) DO UPDATE SET
                        streamer_name = excluded.streamer_name,
                        category = COALESCE($category, favorites.category),
                        updated_at_utc = excluded.updated_at_utc;
                    """;
                command.Parameters.AddWithValue("$platform", FormatPlatform(favorite.Platform));
                command.Parameters.AddWithValue("$channelId", favorite.ChannelId);
                command.Parameters.AddWithValue("$streamerName", favorite.StreamerName);
                command.Parameters.AddWithValue("$category", favorite.Category);
                command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                await command.ExecuteNonQueryAsync(cancellationToken);
                return true;
            },
            cancellationToken);
    }

    public Task DeleteAsync(Platform platform, string channelId, CancellationToken cancellationToken)
    {
        Validate(platform, channelId);
        return ExecuteAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM favorites WHERE platform = $platform AND channel_id = $channelId;";
                command.Parameters.AddWithValue("$platform", FormatPlatform(platform));
                command.Parameters.AddWithValue("$channelId", channelId);
                await command.ExecuteNonQueryAsync(cancellationToken);
                return true;
            },
            cancellationToken);
    }

    public Task UpdateCategoryAsync(Platform platform, string channelId, string category, CancellationToken cancellationToken)
    {
        Validate(platform, channelId);
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("A category is required.", nameof(category));
        }

        return ExecuteAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    UPDATE favorites
                    SET category = $category, updated_at_utc = $now
                    WHERE platform = $platform AND channel_id = $channelId;
                    """;
                command.Parameters.AddWithValue("$category", category.Trim());
                command.Parameters.AddWithValue("$platform", FormatPlatform(platform));
                command.Parameters.AddWithValue("$channelId", channelId);
                command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                await command.ExecuteNonQueryAsync(cancellationToken);
                return true;
            },
            cancellationToken);
    }

    private async Task<T> ExecuteAsync<T>(Func<SqliteConnection, Task<T>> action, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                return await action(connection);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && attempt < MaximumAttempts)
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

    internal static string FormatPlatform(Platform platform)
    {
        return platform switch
        {
            Platform.Chzzk => "chzzk",
            Platform.Soop => "soop",
            _ => throw new ArgumentOutOfRangeException(nameof(platform)),
        };
    }

    internal static bool TryParsePlatform(string value, out Platform platform)
    {
        platform = value switch
        {
            "chzzk" => Platform.Chzzk,
            "soop" => Platform.Soop,
            _ => default,
        };
        return value is "chzzk" or "soop";
    }
}
