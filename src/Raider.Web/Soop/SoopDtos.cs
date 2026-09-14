using System.Text.Json.Serialization;

namespace Raider.Web.Soop;

internal sealed record SoopResponse(
    [property: JsonPropertyName("total_cnt")] int? TotalCount,
    [property: JsonPropertyName("page_no")] string? PageNumber,
    [property: JsonPropertyName("result")] int? Result,
    [property: JsonPropertyName("broad")] SoopBroadcast[]? Broadcasts);

internal sealed record SoopBroadcast(
    [property: JsonPropertyName("broad_no")] string? BroadcastNumber,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("user_nick")] string? UserNickname,
    [property: JsonPropertyName("broad_title")] string? Title,
    [property: JsonPropertyName("broad_thumb")] string? Thumbnail,
    [property: JsonPropertyName("total_view_cnt")] string? TotalViewCount,
    [property: JsonPropertyName("broad_cate_no")] string? CategoryNumber);

internal sealed record SoopCategoryResponse(
    [property: JsonPropertyName("broad_category")] SoopCategory[]? Categories);

internal sealed record SoopCategory(
    [property: JsonPropertyName("cate_name")] string? Name,
    [property: JsonPropertyName("cate_no")] string? Number,
    [property: JsonPropertyName("child")] SoopCategory[]? Children);
