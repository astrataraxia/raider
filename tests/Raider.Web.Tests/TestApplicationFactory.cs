// 웹 통합 테스트를 실제 외부 인증정보와 수집 실행에서 격리한다.
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Raider.Web.Tests;

internal sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Raider:Chzzk:ClientId"] = "fixture-client-id",
                ["Raider:Chzzk:ClientSecret"] = "fixture-client-secret",
                ["Raider:Soop:ClientId"] = "fixture-soop-client-id",
                ["Raider:Collection:Chzzk:Enabled"] = "false",
                ["Raider:Collection:Soop:Enabled"] = "false",
                ["Raider:Favorites:DatabasePath"] = Path.Combine(Path.GetTempPath(), $"raider-test-{Guid.NewGuid():N}.db"),
            });
        });
    }
}
