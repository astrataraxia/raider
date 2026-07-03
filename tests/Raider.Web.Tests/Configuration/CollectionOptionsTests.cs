// 수집 옵션의 운영 기본값이 외부 API 규모에 맞는지 검증한다.
using Microsoft.Extensions.Configuration;
using Raider.Web.Collection;

namespace Raider.Web.Tests.Configuration;

public sealed class CollectionOptionsTests
{
    [Fact]
    public void ChzzkCollectionTimeoutAllowsLargePaginatedCatalog()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .Build();

        var options = configuration
            .GetSection("Raider:Collection:Chzzk")
            .Get<CollectionOptions>();

        Assert.NotNull(options);
        Assert.True(options.CollectionTimeout >= TimeSpan.FromMinutes(3));
    }
}
