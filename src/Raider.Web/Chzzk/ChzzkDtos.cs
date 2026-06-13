// CHZZK 공식 API 응답을 어댑터 내부에서 역직렬화한다.
using System.Text.Json.Serialization;

namespace Raider.Web.Chzzk;

internal sealed record ChzzkResponse(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("content")] ChzzkContent? Content);

internal sealed record ChzzkContent(
    [property: JsonPropertyName("data")] ChzzkLive[] Data,
    [property: JsonPropertyName("page")] ChzzkPage Page);

internal sealed record ChzzkPage(
    [property: JsonPropertyName("next")] string? Next);

internal sealed record ChzzkLive(
    [property: JsonPropertyName("liveId")] long LiveId,
    [property: JsonPropertyName("liveTitle")] string? LiveTitle,
    [property: JsonPropertyName("liveThumbnailImageUrl")] string? LiveThumbnailImageUrl,
    [property: JsonPropertyName("concurrentUserCount")] int ConcurrentUserCount,
    [property: JsonPropertyName("tags")] string[]? Tags,
    [property: JsonPropertyName("categoryType")] string? CategoryType,
    [property: JsonPropertyName("liveCategory")] string? LiveCategory,
    [property: JsonPropertyName("liveCategoryValue")] string? LiveCategoryValue,
    [property: JsonPropertyName("channelId")] string? ChannelId,
    [property: JsonPropertyName("channelName")] string? ChannelName);
