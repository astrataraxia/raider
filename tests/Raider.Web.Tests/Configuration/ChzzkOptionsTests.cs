// CHZZK 비밀 설정의 바인딩, 검증, 출력 방지 계약을 검증한다.
using Microsoft.Extensions.Configuration;
using Raider.Web.Configuration;

namespace Raider.Web.Tests.Configuration;

public sealed class ChzzkOptionsTests
{
    [Fact]
    public void HierarchicalEnvironmentVariablesOverrideDevelopmentSecrets()
    {
        const string prefix = "RAIDER_TEST_";
        const string idVariable = $"{prefix}RAIDER__CHZZK__CLIENTID";
        const string secretVariable = $"{prefix}RAIDER__CHZZK__CLIENTSECRET";

        Environment.SetEnvironmentVariable(idVariable, "environment-id");
        Environment.SetEnvironmentVariable(secretVariable, "environment-secret");

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Raider:Chzzk:ClientId"] = "development-secret-id",
                    ["Raider:Chzzk:ClientSecret"] = "development-secret-value",
                })
                .AddEnvironmentVariables(prefix)
                .Build();

            var options = configuration.GetSection(ChzzkOptions.SectionName).Get<ChzzkOptions>();

            Assert.NotNull(options);
            Assert.Equal("environment-id", options.ClientId);
            Assert.Equal("environment-secret", options.ClientSecret);
        }
        finally
        {
            Environment.SetEnvironmentVariable(idVariable, null);
            Environment.SetEnvironmentVariable(secretVariable, null);
        }
    }

    [Fact]
    public void ValidationAndStringOutputDoNotExposeSecrets()
    {
        var options = new ChzzkOptions
        {
            ClientId = "private-client-id",
            ClientSecret = "private-client-secret",
        };

        options.Validate();

        Assert.DoesNotContain(options.ClientId, options.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(options.ClientSecret, options.ToString(), StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => new ChzzkOptions().Validate());
    }
}
