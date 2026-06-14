// Raider 생존 상태 엔드포인트의 HTTP 계약을 검증한다.
using System.Net;
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

    public void Dispose()
    {
        client.Dispose();
        application.Dispose();
    }
}
