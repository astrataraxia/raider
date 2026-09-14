using Raider.Web.Configuration;

namespace Raider.Web.Tests.Configuration;

public sealed class SoopOptionsTests
{
    [Fact]
    public void IsConfiguredRequiresClientId()
    {
        Assert.True(new SoopOptions { ClientId = "private-client-id" }.IsConfigured);
        Assert.False(new SoopOptions().IsConfigured);
    }
}
