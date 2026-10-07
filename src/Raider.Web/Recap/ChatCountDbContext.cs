using Microsoft.EntityFrameworkCore;

namespace Raider.Web.Recap;

public class ChatCountDbContext : DbContext
{
    private readonly string databasePath;

    public ChatCountDbContext(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("A database path is required.", nameof(databasePath));
        }

        this.databasePath = databasePath;
    }

    public DbSet<ChatDayCount> ChatDayCounts => Set<ChatDayCount>();
    public DbSet<BroadcastDay> BroadcastDays => Set<BroadcastDay>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        optionsBuilder.UseSqlite(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
                DefaultTimeout = 2,
            }.ToString());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatDayCount>(entity =>
        {
            entity.ToTable("chat_day_counts");
            entity.HasKey(e => new { e.ChannelId, e.SenderChannelId, e.Date });

            entity.Property(e => e.ChannelId).IsRequired().HasColumnName("channel_id");
            entity.Property(e => e.SenderChannelId).IsRequired().HasColumnName("sender_channel_id");
            entity.Property(e => e.Date).IsRequired().HasColumnName("date_kst");
            entity.Property(e => e.Count).IsRequired().HasColumnName("count");
        });

        modelBuilder.Entity<BroadcastDay>(entity =>
        {
            entity.ToTable("broadcast_days");
            entity.HasKey(e => new { e.ChannelId, e.Date });

            entity.Property(e => e.ChannelId).IsRequired().HasColumnName("channel_id");
            entity.Property(e => e.Date).IsRequired().HasColumnName("date_kst");
        });
    }

    public static string FormatDate(DateOnly date)
        => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public static DateOnly ParseDate(string value)
        => DateOnly.ParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
