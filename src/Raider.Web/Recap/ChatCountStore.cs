// 즐겨찾기 CHZZK 채팅의 날짜 건수와 방송일을 SQLite에 저장한다.
using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;

namespace Raider.Web.Recap;

public sealed class ChatCountStore
{
    private const int MaximumAttempts = 3;
    private readonly ChatCountDbContext dbContext;

    public ChatCountStore(string databasePath)
    {
        dbContext = new ChatCountDbContext(databasePath);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);

                // Migration: add tables if they don't exist (for existing databases with favorites table)
                try
                {
                    var connection = dbContext.Database.GetDbConnection();
                    await connection.OpenAsync(cancellationToken);
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
                }
                catch (Microsoft.Data.Sqlite.SqliteException)
                {
                    // Tables already exist, ignore
                }

                return;
            }
            catch (Microsoft.Data.Sqlite.SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    public Task AddChatAsync(string channelId, string senderChannelId, DateOnly date, CancellationToken cancellationToken)
        => AddChatCountsAsync([new ChatCountDelta(channelId, senderChannelId, date, 1)], cancellationToken);

    public async Task AddChatCountsAsync(IReadOnlyList<ChatCountDelta> counts, CancellationToken cancellationToken)
    {
        if (counts.Count == 0)
        {
            return;
        }

        foreach (var count in counts)
        {
            RequireId(count.ChannelId, nameof(count.ChannelId));
            RequireId(count.SenderChannelId, nameof(count.SenderChannelId));
            if (count.Count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(counts), "A chat count must be positive.");
            }
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                foreach (var count in counts)
                {
                    var existing = await dbContext.ChatDayCounts.FindAsync(
                        new object[] { count.ChannelId, count.SenderChannelId, count.Date },
                        cancellationToken);

                    if (existing is not null)
                    {
                        existing.Count += count.Count;
                    }
                    else
                    {
                        dbContext.ChatDayCounts.Add(new ChatDayCount(
                            count.ChannelId,
                            count.SenderChannelId,
                            count.Date,
                            count.Count));
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (Microsoft.Data.Sqlite.SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    public async Task RecordBroadcastDayAsync(string channelId, DateOnly date, CancellationToken cancellationToken)
    {
        RequireId(channelId, nameof(channelId));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var existing = await dbContext.BroadcastDays.FindAsync(
                    new object[] { channelId, date },
                    cancellationToken);

                if (existing is null)
                {
                    dbContext.BroadcastDays.Add(new BroadcastDay(channelId, date));
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

    public async Task<ImmutableArray<ChatDayCount>> ListViewerDaysAsync(string senderChannelId, CancellationToken cancellationToken)
    {
        RequireId(senderChannelId, nameof(senderChannelId));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var results = await dbContext.ChatDayCounts
                    .Where(c => c.SenderChannelId == senderChannelId)
                    .OrderBy(c => c.Date)
                    .ThenBy(c => c.ChannelId)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                return results.ToImmutableArray();
            }
            catch (Microsoft.Data.Sqlite.SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    public async Task<ImmutableArray<DateOnly>> ListBroadcastDaysAsync(string channelId, CancellationToken cancellationToken)
    {
        RequireId(channelId, nameof(channelId));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var results = await dbContext.BroadcastDays
                    .Where(b => b.ChannelId == channelId)
                    .OrderBy(b => b.Date)
                    .AsNoTracking()
                    .Select(b => b.Date)
                    .ToListAsync(cancellationToken);

                return results.ToImmutableArray();
            }
            catch (Microsoft.Data.Sqlite.SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    public async Task<ImmutableArray<ChannelFirstSeen>> ListFirstSeenAsync(string channelId, CancellationToken cancellationToken)
    {
        RequireId(channelId, nameof(channelId));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var results = await dbContext.ChatDayCounts
                    .Where(c => c.ChannelId == channelId)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                var grouped = results
                    .GroupBy(c => c.SenderChannelId)
                    .Select(g => new ChannelFirstSeen(
                        g.Key,
                        g.Min(c => c.Date)))
                    .OrderBy(c => c.FirstSeen)
                    .ThenBy(c => c.SenderChannelId)
                    .ToImmutableArray();

                return grouped;
            }
            catch (Microsoft.Data.Sqlite.SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && attempt < MaximumAttempts)
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
}
