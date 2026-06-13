// SOOP 공개 웹 JSON 응답을 어댑터 내부에서 역직렬화한다.
using System.Text.Json.Serialization;

namespace Raider.Web.Soop;

internal sealed record SoopResponse(
    [property: JsonPropertyName("total_cnt")] int? TotalCount,
    [property: JsonPropertyName("cnt")] int? Count,
    [property: JsonPropertyName("broad")] SoopBroadcast[]? Broadcasts);

internal sealed record SoopBroadcast(
    [property: JsonPropertyName("broad_no")] long BroadcastNumber,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("user_nick")] string? UserNickname,
    [property: JsonPropertyName("broad_title")] string? Title,
    [property: JsonPropertyName("broad_thumb")] string? Thumbnail,
    [property: JsonPropertyName("current_view_cnt")] int CurrentViewerCount,
    [property: JsonPropertyName("auto_hashtags")] string[]? AutoHashtags,
    [property: JsonPropertyName("category_tags")] string[]? CategoryTags,
    [property: JsonPropertyName("hash_tags")] string[]? HashTags,
    [property: JsonPropertyName("lang_tags")] string[]? LanguageTags,
    [property: JsonPropertyName("category_name")] string? CategoryName);
