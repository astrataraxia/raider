// SOOP 공식 API JSON 응답을 어댑터 내부에서 역직렬화한다.
using System.Text.Json.Serialization;

namespace Raider.Web.Soop;

internal sealed record SoopResponse(
    [property: JsonPropertyName("total_cnt")] int? TotalCount,
    [property: JsonPropertyName("page_no")] string? PageNumber,
    [property: JsonPropertyName("time")] long? Time,
    [property: JsonPropertyName("result")] int? Result,
    [property: JsonPropertyName("msg")] string? Message,
    [property: JsonPropertyName("broad")] SoopBroadcast[]? Broadcasts);

internal sealed record SoopBroadcast(
    [property: JsonPropertyName("broad_no")] string? BroadcastNumber,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("user_nick")] string? UserNickname,
    [property: JsonPropertyName("broad_title")] string? Title,
    [property: JsonPropertyName("broad_thumb")] string? Thumbnail,
    [property: JsonPropertyName("total_view_cnt")] string? TotalViewCount,
    [property: JsonPropertyName("broad_cate_no")] string? CategoryNumber,
    [property: JsonPropertyName("broad_start")] string? StartTime,
    [property: JsonPropertyName("broad_grade")] string? Grade,
    [property: JsonPropertyName("broad_bps")] string? BitsPerSecond,
    [property: JsonPropertyName("broad_resolution")] string? Resolution,
    [property: JsonPropertyName("is_password")] string? IsPassword,
    [property: JsonPropertyName("visit_broad_type")] string? VisitBroadType,
    [property: JsonPropertyName("profile_img")] string? ProfileImage,
    [property: JsonPropertyName("paid_promotion")] int? PaidPromotion);

internal sealed record SoopCategoryResponse(
    [property: JsonPropertyName("broad_category")] SoopCategory[]? Categories);

internal sealed record SoopCategory(
    [property: JsonPropertyName("cate_name")] string? Name,
    [property: JsonPropertyName("cate_no")] string? Number,
    [property: JsonPropertyName("child")] SoopCategory[]? Children);
