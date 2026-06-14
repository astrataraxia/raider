// 영구 저장하는 공용 방송인 즐겨찾기 정보를 표현한다.
using Raider.Web.Live;

namespace Raider.Web.Favorites;

public sealed record Favorite(Platform Platform, string ChannelId, string StreamerName);
