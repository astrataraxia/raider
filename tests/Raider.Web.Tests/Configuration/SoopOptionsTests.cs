// SOOP 설정의 구성 여부와 출력 방지 계약을 검증한다.
using Raider.Web.Configuration;

namespace Raider.Web.Tests.Configuration;

public sealed class SoopOptionsTests
{
    [Fact]
    public void ValidationAndStringOutputDoNotExposeSecrets()
    {
        var options = new SoopOptions
        {
            ClientId = "private-client-id",
        };

        options.Validate();

        Assert.True(options.IsConfigured);
        Assert.DoesNotContain(options.ClientId, options.ToString(), StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => new SoopOptions().Validate());
        Assert.False(new SoopOptions().IsConfigured);
    }
}
