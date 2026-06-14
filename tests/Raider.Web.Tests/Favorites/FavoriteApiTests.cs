// 공용 즐겨찾기 API와 현재 라이브 상태 결합 계약을 검증한다.
using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Raider.Web.Collection;
using Raider.Web.Live;

namespace Raider.Web.Tests.Favorites;

public sealed class FavoriteApiTests : IDisposable
{
    private readonly TestApplicationFactory application = new();
    private readonly HttpClient client;
    private readonly SnapshotStore snapshots;

    public FavoriteApiTests()
    {
        client = application.CreateClient();
        snapshots = application.Services.GetRequiredService<SnapshotStore>();
    }

    [Fact]
    public async Task PutRejectsUnknownChannelAndSharesFavoriteThroughGet()
    {
        snapshots.ApplySuccess(Platform.Chzzk, [Stream("live", "channel-1", "Alpha")], DateTimeOffset.UtcNow);
        snapshots.ApplySuccess(Platform.Soop, [], DateTimeOffset.UtcNow.AddTicks(1));
        var token = await AntiForgeryTokenAsync();

        using var unknown = new HttpRequestMessage(HttpMethod.Put, "/api/favorites/chzzk/not-found");
        unknown.Headers.Add("RequestVerificationToken", token);
        using var unknownResponse = await client.SendAsync(unknown);

        using var add = new HttpRequestMessage(HttpMethod.Put, "/api/favorites/chzzk/channel-1");
        add.Headers.Add("RequestVerificationToken", token);
        using var addResponse = await client.SendAsync(add);
        var favorites = await client.GetFromJsonAsync<FavoriteResponse[]>("/api/favorites");

        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);
        var favorite = Assert.Single(favorites!);
        Assert.Equal("Alpha", favorite.StreamerName);
        Assert.Equal("live", favorite.Status);
        Assert.Equal("https://example.invalid/live", favorite.WatchUrl);
        Assert.Equal(1, favorite.ViewerCount);
        Assert.Equal("기본", favorite.Category);
    }

    [Fact]
    public async Task FailedPlatformReportsDelayedInsteadOfLive()
    {
        snapshots.ApplySuccess(Platform.Chzzk, [Stream("live", "channel-1", "Alpha")], DateTimeOffset.UtcNow);
        snapshots.ApplySuccess(Platform.Soop, [], DateTimeOffset.UtcNow.AddTicks(1));
        var token = await AntiForgeryTokenAsync();
        using var add = new HttpRequestMessage(HttpMethod.Put, "/api/favorites/chzzk/channel-1");
        add.Headers.Add("RequestVerificationToken", token);
        using var addResponse = await client.SendAsync(add);
        addResponse.EnsureSuccessStatusCode();
        snapshots.ApplyFailure(Platform.Chzzk, new PlatformError(PlatformErrorKind.Network), DateTimeOffset.UtcNow.AddTicks(2));

        var favorites = await client.GetFromJsonAsync<FavoriteResponse[]>("/api/favorites");

        Assert.Equal("delayed", Assert.Single(favorites!).Status);
    }

    [Fact]
    public async Task PutWithoutAntiForgeryTokenIsRejected()
    {
        snapshots.ApplySuccess(Platform.Chzzk, [Stream("live", "channel-1", "Alpha")], DateTimeOffset.UtcNow);

        using var response = await client.PutAsync("/api/favorites/chzzk/channel-1", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HealthyCollectionReportsMissingFavoriteOfflineAndSortsLiveFirst()
    {
        snapshots.ApplySuccess(
            Platform.Chzzk,
            [Stream("alpha-live", "alpha", "Alpha"), Stream("beta-live", "beta", "Beta")],
            DateTimeOffset.UtcNow);
        snapshots.ApplySuccess(Platform.Soop, [], DateTimeOffset.UtcNow.AddTicks(1));
        var token = await AntiForgeryTokenAsync();
        await PutFavoriteAsync("alpha", token);
        await PutFavoriteAsync("beta", token);
        snapshots.ApplySuccess(Platform.Chzzk, [Stream("beta-new", "beta", "Beta")], DateTimeOffset.UtcNow.AddTicks(2));

        var favorites = await client.GetFromJsonAsync<FavoriteResponse[]>("/api/favorites");

        var result = Assert.IsType<FavoriteResponse[]>(favorites);
        Assert.Equal(["live", "offline"], result.Select(favorite => favorite.Status));
        Assert.Equal(["Beta", "Alpha"], result.Select(favorite => favorite.StreamerName));
    }

    private async Task<string> AntiForgeryTokenAsync()
    {
        var html = await client.GetStringAsync("/", CancellationToken.None);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        return WebUtility.HtmlDecode(Assert.IsType<Match>(match).Groups[1].Value);
    }

    private async Task PutFavoriteAsync(string channelId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/favorites/chzzk/{channelId}");
        request.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task UpdateCategoryUpdatesCategoryAndIsProtectedByAntiforgery()
    {
        snapshots.ApplySuccess(Platform.Chzzk, [Stream("live", "channel-1", "Alpha")], DateTimeOffset.UtcNow);
        snapshots.ApplySuccess(Platform.Soop, [], DateTimeOffset.UtcNow.AddTicks(1));
        var token = await AntiForgeryTokenAsync();
        await PutFavoriteAsync("channel-1", token);

        // Try updating category without antiforgery token
        using var noTokenRequest = new HttpRequestMessage(HttpMethod.Put, "/api/favorites/chzzk/channel-1/category");
        noTokenRequest.Content = JsonContent.Create(new { category = "Gaming" });
        using var noTokenResponse = await client.SendAsync(noTokenRequest);
        Assert.Equal(HttpStatusCode.BadRequest, noTokenResponse.StatusCode);

        // Update with antiforgery token
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/favorites/chzzk/channel-1/category");
        request.Headers.Add("RequestVerificationToken", token);
        request.Content = JsonContent.Create(new { category = "Gaming" });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify it was updated
        var favorites = await client.GetFromJsonAsync<FavoriteResponse[]>("/api/favorites");
        var favorite = Assert.Single(favorites!);
        Assert.Equal("Gaming", favorite.Category);
    }

    public void Dispose()
    {
        client.Dispose();
        application.Dispose();
    }

    private static LiveStream Stream(string broadcastId, string channelId, string name)
    {
        return LiveStream.Create(
            Platform.Chzzk,
            broadcastId,
            channelId,
            name,
            "Live",
            1,
            null,
            $"https://example.invalid/{broadcastId}",
            ImmutableArray<string>.Empty,
            DateTimeOffset.UtcNow);
    }

    private sealed record FavoriteResponse(string StreamerName, string Status, string? WatchUrl, int? ViewerCount, string Category);
}
