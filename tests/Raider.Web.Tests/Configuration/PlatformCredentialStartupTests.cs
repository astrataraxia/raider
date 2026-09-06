// 한쪽 플랫폼 키가 없어도 프로세스가 시작되고 해당 플랫폼만 Configuration 오류가 된다.
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Raider.Web.Chzzk;
using Raider.Web.Collection;
using Raider.Web.Soop;

namespace Raider.Web.Tests.Configuration;

public sealed class PlatformCredentialStartupTests
{
    [Fact]
    public async Task StartsAndServesLiveHealthWhenChzzkCredentialsAreMissing()
    {
        using var application = CreateApplication(includeChzzk: false, includeSoop: true);
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/health/live", CancellationToken.None);
        var chzzk = application.Services.GetRequiredService<ChzzkClient>();
        var error = await Assert.ThrowsAsync<PlatformCollectionException>(
            () => chzzk.CollectAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PlatformErrorKind.Configuration, error.Error.Kind);
    }

    [Fact]
    public async Task StartsAndServesLiveHealthWhenSoopCredentialsAreMissing()
    {
        using var application = CreateApplication(includeChzzk: true, includeSoop: false);
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/health/live", CancellationToken.None);
        var soop = application.Services.GetRequiredService<SoopClient>();
        var error = await Assert.ThrowsAsync<PlatformCollectionException>(
            () => soop.CollectAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PlatformErrorKind.Configuration, error.Error.Kind);
    }

    private static WebApplicationFactory<Program> CreateApplication(bool includeChzzk, bool includeSoop)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(configuration =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["Raider:Chzzk:ClientId"] = includeChzzk ? "fixture-id" : "",
                    ["Raider:Chzzk:ClientSecret"] = includeChzzk ? "fixture-secret" : "",
                    ["Raider:Soop:ClientId"] = includeSoop ? "fixture-soop-client-id" : "",
                    ["Raider:Collection:Chzzk:Enabled"] = "false",
                    ["Raider:Collection:Soop:Enabled"] = "false",
                    ["Raider:Favorites:DatabasePath"] = Path.Combine(Path.GetTempPath(), $"raider-test-{Guid.NewGuid():N}.db"),
                };

                configuration.AddInMemoryCollection(values);
            });
        });
    }
}
