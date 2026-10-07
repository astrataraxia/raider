using Microsoft.EntityFrameworkCore;
using Raider.Web.Live;

namespace Raider.Web.Favorites;

public class FavoriteDbContext : DbContext
{
    public FavoriteDbContext(DbContextOptions<FavoriteDbContext> options)
        : base(options)
    {
    }

    public DbSet<Favorite> Favorites => Set<Favorite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(e => new { e.Platform, e.ChannelId });

            entity.Property(e => e.Platform)
                .HasConversion(
                    p => FormatPlatform(p),
                    v => ParsePlatform(v));

            entity.Property(e => e.ChannelId).IsRequired().HasColumnName("channel_id");
            entity.Property(e => e.StreamerName).IsRequired().HasColumnName("streamer_name");
            entity.Property(e => e.Category).IsRequired().HasDefaultValue("기본").HasColumnName("category");

            entity.Property(e => e.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
            entity.Property(e => e.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");
        });
    }

    public static string FormatPlatform(Platform platform)
    {
        return platform switch
        {
            Platform.Chzzk => "chzzk",
            Platform.Soop => "soop",
            _ => throw new ArgumentOutOfRangeException(nameof(platform)),
        };
    }

    private static Platform ParsePlatform(string value)
    {
        return value switch
        {
            "chzzk" => Platform.Chzzk,
            "soop" => Platform.Soop,
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };
    }

    public static bool TryParsePlatform(string value, out Platform platform)
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
