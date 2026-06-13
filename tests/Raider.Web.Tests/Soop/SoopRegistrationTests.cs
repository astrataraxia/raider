// ASP.NET Core 의존성 주입에서 SOOP typed client와 무쿠키 경계를 검증한다.
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Raider.Web.Collection;
using Raider.Web.Live;

namespace Raider.Web.Tests.Soop;

public sealed class SoopRegistrationTests
{
    [Fact]
    public void ResolvesBothPlatformSources()
    {
        using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration(configuration =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Raider:Chzzk:ClientId"] = "fixture-id",
                        ["Raider:Chzzk:ClientSecret"] = "fixture-secret",
                        ["Raider:Collection:Chzzk:Enabled"] = "false",
                        ["Raider:Collection:Soop:Enabled"] = "false",
                    });
                });
            });
        using var scope = application.Services.CreateScope();

        var platforms = scope.ServiceProvider.GetServices<ILiveSource>().Select(source => source.Platform).ToArray();

        Assert.Equal([Platform.Chzzk, Platform.Soop], platforms);
    }
}
