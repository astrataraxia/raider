// 치지직 로그인 이용자의 채팅 리캡을 SQLite 집계로 만든다.
using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Raider.Web.Favorites;
using Raider.Web.Recap;

namespace Raider.Web.Pages;

public sealed class RecapModel(ChatCountStore store, FavoriteStore favorites) : PageModel
{
    public ViewerRecap Recap { get; private set; } = ViewerRecap.Empty;

    public string ViewerName { get; private set; } = string.Empty;

    public DateOnly? CollectionStartedOn { get; private set; }

    public bool IsSignedIn { get; private set; }

    public bool LoginFailed { get; private set; }

    public async Task OnGetAsync(string? login, CancellationToken cancellationToken)
    {
        LoginFailed = string.Equals(login, "failed", StringComparison.Ordinal);
        var channelId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewerName = User.Identity?.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return;
        }

        IsSignedIn = true;

        var names = (await favorites.ListAsync(cancellationToken))
            .GroupBy(favorite => favorite.ChannelId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().StreamerName, StringComparer.Ordinal);
        var days = await store.ListViewerDaysAsync(channelId, cancellationToken);
        CollectionStartedOn = days.IsDefaultOrEmpty ? null : days.Min(day => day.Date);
        var homeId = days
            .GroupBy(day => day.ChannelId, StringComparer.Ordinal)
            .OrderByDescending(group => group.Sum(day => day.Count))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault();
        var broadcasts = homeId is null
            ? ImmutableArray<DateOnly>.Empty
            : await store.ListBroadcastDaysAsync(homeId, cancellationToken);
        var firstSeen = homeId is null
            ? ImmutableArray<ChannelFirstSeen>.Empty
            : await store.ListFirstSeenAsync(homeId, cancellationToken);
        Recap = ViewerRecap.ForViewer(channelId, days, broadcasts, firstSeen, names);
    }
}
