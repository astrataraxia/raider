// 수집 옵션의 운영 기본값이 외부 API 규모에 맞는지 검증한다.
using Microsoft.Extensions.Configuration;
using Raider.Web.Collection;

namespace Raider.Web.Tests.Configuration;

public sealed class CollectionOptionsTests
{
    [Fact]
    public void ChzzkCollectionTimeoutAllowsLargePaginatedCatalog()
    {
        var options = ReadCollectionOptions("Chzzk");

        Assert.NotNull(options);
        Assert.True(options.CollectionTimeout >= TimeSpan.FromMinutes(3));
    }

    [Fact]
    public void SoopCollectionTimeoutAllowsSlowPaginatedCatalog()
    {
        var options = ReadCollectionOptions("Soop");

        Assert.NotNull(options);
        Assert.True(options.CollectionTimeout >= TimeSpan.FromMinutes(3));
    }

    private static CollectionOptions? ReadCollectionOptions(string platform)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .Build();

        return configuration
            .GetSection($"Raider:Collection:{platform}")
            .Get<CollectionOptions>();
    }
}
