// 영구 저장하는 공용 방송인 즐겨찾기 정보를 표현한다.
using Raider.Web.Live;

namespace Raider.Web.Favorites;

public sealed class Favorite
{
    public Platform Platform { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public string StreamerName { get; set; } = string.Empty;
    public string Category { get; set; } = "기본";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Favorite() { }

    public Favorite(Platform platform, string channelId, string streamerName, string category = "기본")
    {
        Platform = platform;
        ChannelId = channelId;
        StreamerName = streamerName;
        Category = category;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
