// 홈, 리캡, 상태가 같이 쓰는 상단 바 인자다.
namespace Raider.Web.Pages;

public sealed record SiteHeader(
    string CurrentPage,
    int? StreamCount,
    bool ShowRefresh,
    bool IsRefreshing,
    bool SyncWarning,
    string? SyncTitle,
    string? SyncDetail,
    bool ShowLogout,
    string? Platform,
    string? Tag,
    string? Query,
    bool FavoritesOnly,
    string? Category);
