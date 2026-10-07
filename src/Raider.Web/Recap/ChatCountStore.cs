// 즐겨찾기 CHZZK 채팅의 날짜 건수와 방송일을 SQLite에 저장한다.
using System.Collections.Immutable;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Raider.Web;

namespace Raider.Web.Recap;

public sealed class ChatCountStore
{
    private readonly IDbContextFactory<ChatCountDbContext> contexts;

    public ChatCountStore(IDbContextFactory<ChatCountDbContext> contexts)
    {
        this.contexts = contexts;
    }

    public ChatCountStore(string databasePath)
        : this(FactoryFor(databasePath))
    {
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
        => SqliteDatabase.ExecuteAsync(contexts, async (db, token) =>
        {
            await db.Database.EnsureCreatedAsync(token);

            // Migration: add tables if they don't exist (for existing databases with favorites table)
            try
            {
                await SqliteDatabase.OpenAsync(db, token);
                await using var command = db.Database.GetDbConnection().CreateCommand();
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
                await command.ExecuteNonQueryAsync(token);
            }
            catch (SqliteException)
            {
                // Tables already exist, ignore
            }
        }, cancellationToken);

    public Task AddChatAsync(string channelId, string senderChannelId, DateOnly date, CancellationToken cancellationToken)
        => AddChatCountsAsync([new ChatCountDelta(channelId, senderChannelId, date, 1)], cancellationToken);

    public Task AddChatCountsAsync(IReadOnlyList<ChatCountDelta> counts, CancellationToken cancellationToken)
    {
        if (counts.Count == 0)
        {
            return Task.CompletedTask;
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

        return SqliteDatabase.ExecuteAsync(contexts, async (db, token) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            foreach (var count in counts)
            {
                var existing = await db.ChatDayCounts.FindAsync(
                    new object[] { count.ChannelId, count.SenderChannelId, count.Date },
                    token);

                if (existing is not null)
                {
                    existing.Count += count.Count;
                }
                else
                {
                    db.ChatDayCounts.Add(new ChatDayCount(
                        count.ChannelId,
                        count.SenderChannelId,
                        count.Date,
                        count.Count));
                }
            }

            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }, cancellationToken);
    }

    public Task RecordBroadcastDayAsync(string channelId, DateOnly date, CancellationToken cancellationToken)
    {
        RequireId(channelId, nameof(channelId));

        return SqliteDatabase.ExecuteAsync(contexts, async (db, token) =>
        {
            var existing = await db.BroadcastDays.FindAsync(
                new object[] { channelId, date },
                token);

            if (existing is null)
            {
                db.BroadcastDays.Add(new BroadcastDay(channelId, date));
                await db.SaveChangesAsync(token);
            }
        }, cancellationToken);
    }

    public Task<ImmutableArray<ChatDayCount>> ListViewerDaysAsync(string senderChannelId, CancellationToken cancellationToken)
    {
        RequireId(senderChannelId, nameof(senderChannelId));

        return SqliteDatabase.ExecuteAsync<ChatCountDbContext, ImmutableArray<ChatDayCount>>(contexts, async (db, token) =>
        {
            var results = await db.ChatDayCounts
                .Where(c => c.SenderChannelId == senderChannelId)
                .OrderBy(c => c.Date)
                .ThenBy(c => c.ChannelId)
                .AsNoTracking()
                .ToListAsync(token);

            return results.ToImmutableArray();
        }, cancellationToken);
    }

    public Task<ImmutableArray<DateOnly>> ListBroadcastDaysAsync(string channelId, CancellationToken cancellationToken)
    {
        RequireId(channelId, nameof(channelId));

        return SqliteDatabase.ExecuteAsync<ChatCountDbContext, ImmutableArray<DateOnly>>(contexts, async (db, token) =>
        {
            var results = await db.BroadcastDays
                .Where(b => b.ChannelId == channelId)
                .OrderBy(b => b.Date)
                .AsNoTracking()
                .Select(b => b.Date)
                .ToListAsync(token);

            return results.ToImmutableArray();
        }, cancellationToken);
    }

    public Task<ImmutableArray<ChannelFirstSeen>> ListFirstSeenAsync(string channelId, CancellationToken cancellationToken)
    {
        RequireId(channelId, nameof(channelId));

        return SqliteDatabase.ExecuteAsync<ChatCountDbContext, ImmutableArray<ChannelFirstSeen>>(contexts, async (db, token) =>
        {
            var results = await db.ChatDayCounts
                .Where(c => c.ChannelId == channelId)
                .AsNoTracking()
                .ToListAsync(token);

            return results
                .GroupBy(c => c.SenderChannelId)
                .Select(g => new ChannelFirstSeen(
                    g.Key,
                    g.Min(c => c.Date)))
                .OrderBy(c => c.FirstSeen)
                .ThenBy(c => c.SenderChannelId)
                .ToImmutableArray();
        }, cancellationToken);
    }

    private static SqliteContextFactory<ChatCountDbContext> FactoryFor(string databasePath)
    {
        var builder = new DbContextOptionsBuilder<ChatCountDbContext>();
        builder.UseRaiderSqlite(databasePath);
        return new SqliteContextFactory<ChatCountDbContext>(() => new ChatCountDbContext(builder.Options));
    }

    private static void RequireId(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty id is required.", name);
        }
    }
}
