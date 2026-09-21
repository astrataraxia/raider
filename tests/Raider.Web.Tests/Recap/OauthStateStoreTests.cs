// OAuth state 발급과 일회 소비 계약을 검증한다.
using Raider.Web.Recap;

namespace Raider.Web.Tests.Recap;

public sealed class OauthStateStoreTests
{
    [Fact]
    public void IssuedStateCanBeConsumedOnce()
    {
        var store = new OauthStateStore();
        var state = store.Issue();

        Assert.True(store.Consume(state));
        Assert.False(store.Consume(state));
        Assert.False(store.Consume("unknown"));
        Assert.False(store.Consume(null));
    }
}
