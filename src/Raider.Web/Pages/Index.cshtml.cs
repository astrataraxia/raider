// 현재 라이브 스냅샷을 검색 가능한 홈 화면 모델로 변환한다.
using System.Collections.Immutable;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Raider.Web.Collection;
using Raider.Web.Favorites;
using Raider.Web.Live;

namespace Raider.Web.Pages;

public sealed class IndexModel(SnapshotStore snapshots, CollectionRegistry registry, FavoriteStore favoriteStore, TimeProvider timeProvider) : PageModel
{
    private const int PageSize = 120;
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(20);

    [BindProperty(SupportsGet = true)]
    public string? Platform { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Tag { get; set; }

    [BindProperty(Name = "q", SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(Name = "p", SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(Name = "fav", SupportsGet = true)]
    public bool FavoritesOnly { get; set; }

    [BindProperty(Name = "cat", SupportsGet = true)]
    public string? SelectedCategory { get; set; }

    public ImmutableArray<LiveStream> Streams { get; private set; } = [];

    public ImmutableArray<string> PopularTags { get; private set; } = [];

    public ImmutableArray<string> AllPopularTags { get; private set; } = [];

    public ImmutableArray<string> FavoriteCategories { get; private set; } = [];

    public CollectionSnapshot Snapshot { get; private set; } = null!;

    public Raider.Web.Live.Platform? SelectedPlatform { get; private set; }

    public int TotalResultCount { get; private set; }

    public long TotalViewers { get; private set; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalResultCount / (double)PageSize));

    public bool IsInitialCollection => Snapshot.Live.Streams.IsEmpty
        && Snapshot.Platforms.Values.All(state => !state.AttemptCompleted);

    public bool HasPartialFailure => Snapshot.Live.Streams.Length > 0
        && Snapshot.Platforms.Values.Any(state => state.Error is not null);

    public bool HasInitialFailure => Snapshot.Live.Streams.Length == 0
        && Snapshot.Platforms.Values.Any(state => state.Error is not null);

    public bool IsStale => Snapshot.Platforms.Values
        .Where(state => state.LastSuccessAt is not null)
        .Any(state => timeProvider.GetUtcNow() - state.LastSuccessAt > StaleAfter);

    public bool IsRefreshing => registry.IsAnyCollecting;

    public async Task OnGetAsync()
    {
        Snapshot = snapshots.Current;
        SelectedPlatform = ParsePlatform(Platform);
        var results = Snapshot.Live.Search(SelectedPlatform, Tag, Query);

        if (FavoritesOnly)
        {
            try
            {
                var favorites = await favoriteStore.ListAsync(HttpContext.RequestAborted);
                FavoriteCategories = favorites
                    .Select(f => f.Category)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c == "기본" ? 0 : 1)
                    .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToImmutableArray();

                var favKeys = favorites.Select(f => (f.Platform, f.ChannelId)).ToHashSet();

                if (!string.IsNullOrWhiteSpace(SelectedCategory))
                {
                    var favsInCat = favorites
                        .Where(f => string.Equals(f.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase))
                        .Select(f => (f.Platform, f.ChannelId))
                        .ToHashSet();

                    results = results
                        .Where(stream => favsInCat.Contains((stream.Platform, stream.ChannelId)))
                        .ToImmutableArray();
                }
                else
                {
                    results = results
                        .Where(stream => favKeys.Contains((stream.Platform, stream.ChannelId)))
                        .ToImmutableArray();
                }
            }
            catch (Exception)
            {
                // Isolate SQLite failure
                results = [];
            }
        }

        TotalResultCount = results.Length;
        TotalViewers = results.Sum(stream => (long)stream.ViewerCount);
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
        Streams = results
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToImmutableArray();
        PopularTags = Snapshot.Live.StreamsByTag
            .OrderByDescending(pair => pair.Value.Length)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(8)
            .Select(pair => pair.Key)
            .ToImmutableArray();
        AllPopularTags = Snapshot.Live.StreamsByTag
            .OrderByDescending(pair => pair.Value.Length)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(30)
            .Select(pair => pair.Key)
            .ToImmutableArray();
    }

    public IActionResult OnPostRefresh()
    {
        registry.TriggerCollectAll();
        return RedirectToPage("/Index", new { platform = Platform, tag = Tag, q = Query, fav = FavoritesOnly ? "true" : null, cat = SelectedCategory });
    }

    private static Raider.Web.Live.Platform? ParsePlatform(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "chzzk" => Raider.Web.Live.Platform.Chzzk,
            "soop" => Raider.Web.Live.Platform.Soop,
            _ => null,
        };
    }
}
