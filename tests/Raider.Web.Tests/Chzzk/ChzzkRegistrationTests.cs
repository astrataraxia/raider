// ASP.NET Core 의존성 주입에서 CHZZK typed client 등록을 검증한다.
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Raider.Web.Collection;
using Raider.Web.Live;

namespace Raider.Web.Tests.Chzzk;

public sealed class ChzzkRegistrationTests
{
    [Fact]
    public void ResolvesChzzkAsLiveSource()
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

        var source = scope.ServiceProvider.GetServices<ILiveSource>().Single(source => source.Platform == Platform.Chzzk);

        Assert.Equal(Platform.Chzzk, source.Platform);
    }
}
