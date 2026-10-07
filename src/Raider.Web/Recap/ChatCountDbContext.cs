using Microsoft.EntityFrameworkCore;

namespace Raider.Web.Recap;

public class ChatCountDbContext : DbContext
{
    public ChatCountDbContext(DbContextOptions<ChatCountDbContext> options)
        : base(options)
    {
    }

    public DbSet<ChatDayCount> ChatDayCounts => Set<ChatDayCount>();
    public DbSet<BroadcastDay> BroadcastDays => Set<BroadcastDay>();

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
