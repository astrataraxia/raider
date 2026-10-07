// SQLite 파일은 작업마다 열고 닫는다. SQLite 연결 풀은 쓰지 않는다.
using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Raider.Web;

internal static class SqliteDatabase
{
    private const int MaximumAttempts = 3;

    public static DbContextOptionsBuilder UseRaiderSqlite(this DbContextOptionsBuilder options, string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("A database path is required.", nameof(databasePath));
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        options.UseSqlite(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 2,
        }.ToString());
        return options;
    }

    public static Task ExecuteAsync<TContext>(
        IDbContextFactory<TContext> contexts,
        Func<TContext, CancellationToken, Task> action,
        CancellationToken cancellationToken)
        where TContext : DbContext
        => ExecuteAsync<TContext, bool>(contexts, async (db, token) =>
        {
            await action(db, token);
            return true;
        }, cancellationToken);

    public static async Task<TResult> ExecuteAsync<TContext, TResult>(
        IDbContextFactory<TContext> contexts,
        Func<TContext, CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var db = await contexts.CreateDbContextAsync(cancellationToken);
                return await action(db, cancellationToken);
            }
            catch (Exception exception) when (IsLocked(exception) && attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    public static async Task OpenAsync(DbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static bool IsLocked(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqlite && sqlite.SqliteErrorCode is 5 or 6)
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class SqliteContextFactory<TContext>(Func<TContext> create) : IDbContextFactory<TContext>
    where TContext : DbContext
{
    public TContext CreateDbContext() => create();

    public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(create());
}
