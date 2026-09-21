// 즐겨찾기 CHZZK 채팅의 날짜 건수와 방송일을 SQLite에 저장한다.
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Raider.Web.Recap;

public sealed class ChatCountStore
{
    private const int MaximumAttempts = 3;
    private readonly string databasePath;
    private readonly string connectionString;

    public ChatCountStore(string databasePath)
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
                    CREATE TABLE IF NOT EXISTS chat_day_counts (
                        channel_id TEXT NOT NULL,
                        sender_channel_id TEXT NOT NULL,
                        date_kst TEXT NOT NULL,
                        count INTEGER NOT NULL,
                        PRIMARY KEY (channel_id, sender_channel_id, date_kst)
                    );
                    CREATE TABLE IF NOT EXISTS broadcast_days (
                        channel_id TEXT NOT NULL,
                        date_kst TEXT NOT NULL,
                        PRIMARY KEY (channel_id, date_kst)
                    );
                    """;
                await command.ExecuteNonQueryAsync(cancellationToken);
                return true;
            },
            cancellationToken);
    }

    public Task AddChatAsync(string channelId, string senderChannelId, DateOnly date, CancellationToken cancellationToken)
    {
        RequireId(channelId, nameof(channelId));
        RequireId(senderChannelId, nameof(senderChannelId));
        return ExecuteAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO chat_day_counts (channel_id, sender_channel_id, date_kst, count)
                    VALUES ($channelId, $senderChannelId, $date, 1)
                    ON CONFLICT(channel_id, sender_channel_id, date_kst) DO UPDATE SET
                        count = count + 1;
                    """;
                command.Parameters.AddWithValue("$channelId", channelId);
                command.Parameters.AddWithValue("$senderChannelId", senderChannelId);
                command.Parameters.AddWithValue("$date", FormatDate(date));
                await command.ExecuteNonQueryAsync(cancellationToken);
                return true;
            },
            cancellationToken);
    }

    public Task RecordBroadcastDayAsync(string channelId, DateOnly date, CancellationToken cancellationToken)
    {
        RequireId(channelId, nameof(channelId));
        return ExecuteAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO broadcast_days (channel_id, date_kst)
                    VALUES ($channelId, $date)
                    ON CONFLICT(channel_id, date_kst) DO NOTHING;
                    """;
                command.Parameters.AddWithValue("$channelId", channelId);
                command.Parameters.AddWithValue("$date", FormatDate(date));
                await command.ExecuteNonQueryAsync(cancellationToken);
                return true;
            },
            cancellationToken);
    }

    public Task<ImmutableArray<ChatDayCount>> ListViewerDaysAsync(string senderChannelId, CancellationToken cancellationToken)
    {
        RequireId(senderChannelId, nameof(senderChannelId));
        return ExecuteAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT channel_id, sender_channel_id, date_kst, count
                    FROM chat_day_counts
                    WHERE sender_channel_id = $senderChannelId
                    ORDER BY date_kst, channel_id;
                    """;
                command.Parameters.AddWithValue("$senderChannelId", senderChannelId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var rows = ImmutableArray.CreateBuilder<ChatDayCount>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add(new ChatDayCount(
                        reader.GetString(0),
                        reader.GetString(1),
                        ParseDate(reader.GetString(2)),
                        reader.GetInt32(3)));
                }

                return rows.ToImmutable();
            },
            cancellationToken);
    }

    public Task<ImmutableArray<DateOnly>> ListBroadcastDaysAsync(string channelId, CancellationToken cancellationToken)
    {
        RequireId(channelId, nameof(channelId));
        return ExecuteAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT date_kst
                    FROM broadcast_days
                    WHERE channel_id = $channelId
                    ORDER BY date_kst;
                    """;
                command.Parameters.AddWithValue("$channelId", channelId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var rows = ImmutableArray.CreateBuilder<DateOnly>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add(ParseDate(reader.GetString(0)));
                }

                return rows.ToImmutable();
            },
            cancellationToken);
    }

    public Task<ImmutableArray<ChannelFirstSeen>> ListFirstSeenAsync(string channelId, CancellationToken cancellationToken)
    {
        RequireId(channelId, nameof(channelId));
        return ExecuteAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT sender_channel_id, MIN(date_kst)
                    FROM chat_day_counts
                    WHERE channel_id = $channelId
                    GROUP BY sender_channel_id
                    ORDER BY MIN(date_kst), sender_channel_id;
                    """;
                command.Parameters.AddWithValue("$channelId", channelId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var rows = ImmutableArray.CreateBuilder<ChannelFirstSeen>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add(new ChannelFirstSeen(reader.GetString(0), ParseDate(reader.GetString(1))));
                }

                return rows.ToImmutable();
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

    private static void RequireId(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty id is required.", name);
        }
    }

    private static string FormatDate(DateOnly date)
        => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly ParseDate(string value)
        => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
