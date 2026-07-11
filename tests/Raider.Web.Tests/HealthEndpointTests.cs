// Raider 생존 상태 엔드포인트의 HTTP 계약을 검증한다.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Raider.Web.Collection;
using Raider.Web.Live;

namespace Raider.Web.Tests;

public sealed class HealthEndpointTests : IDisposable
{
    private readonly WebApplicationFactory<Program> application;
    private readonly HttpClient client;

    public HealthEndpointTests()
    {
        application = new TestApplicationFactory();
        client = application.CreateClient();
    }

    [Fact]
    public async Task LiveHealthReturnsOk()
    {
        using var response = await client.GetAsync("/health/live", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadyHealthChangesAfterBothPlatformsAttemptCollection()
    {
        using var before = await client.GetAsync("/health/ready", CancellationToken.None);
        var snapshots = application.Services.GetRequiredService<SnapshotStore>();

        snapshots.ApplyFailure(Platform.Chzzk, new PlatformError(PlatformErrorKind.Configuration), DateTimeOffset.UtcNow);
        snapshots.ApplyFailure(Platform.Soop, new PlatformError(PlatformErrorKind.Contract), DateTimeOffset.UtcNow.AddTicks(1));

        using var after = await client.GetAsync("/health/ready", CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task RefreshStatusChangesSnapshotVersionWhenSnapshotChanges()
    {
        var snapshots = application.Services.GetRequiredService<SnapshotStore>();
        var before = await client.GetFromJsonAsync<RefreshStatus>("/api/refresh/status");

        snapshots.ApplySuccess(Platform.Chzzk, [], DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(1234));
        var after = await client.GetFromJsonAsync<RefreshStatus>("/api/refresh/status");

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.NotEqual(before.SnapshotVersion, after.SnapshotVersion);
        var chzzk = Assert.Single(after.Platforms, platform => platform.Platform == "Chzzk");
        Assert.Equal("Success", chzzk.Result);
        Assert.Equal(1234, chzzk.DurationMs);
        Assert.Null(chzzk.ErrorKind);
    }

    [Fact]
    public async Task RefreshStatusExposesSafeFailureDiagnostics()
    {
        var snapshots = application.Services.GetRequiredService<SnapshotStore>();

        snapshots.ApplyFailure(
            Platform.Soop,
            new PlatformError(PlatformErrorKind.Timeout),
            DateTimeOffset.UtcNow,
            TimeSpan.FromMilliseconds(2500));
        var status = await client.GetFromJsonAsync<RefreshStatus>("/api/refresh/status");

        Assert.NotNull(status);
        var soop = Assert.Single(status.Platforms, platform => platform.Platform == "Soop");
        Assert.Equal("Failure", soop.Result);
        Assert.Equal(2500, soop.DurationMs);
        Assert.Equal("Timeout", soop.ErrorKind);
    }

    public void Dispose()
    {
        client.Dispose();
        application.Dispose();
    }

    private sealed record RefreshStatus(bool IsRefreshing, string SnapshotVersion, IReadOnlyList<PlatformStatus> Platforms);

    private sealed record PlatformStatus(string Platform, string Result, double? DurationMs, string? ErrorKind);
}
