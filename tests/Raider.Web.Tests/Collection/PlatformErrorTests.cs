// 플랫폼 수집 오류의 즉시 재시도 가능 여부를 검증한다.
using Raider.Web.Collection;

namespace Raider.Web.Tests.Collection;

public sealed class PlatformErrorTests
{
    [Theory]
    [InlineData(PlatformErrorKind.Network, true)]
    [InlineData(PlatformErrorKind.Server, true)]
    [InlineData(PlatformErrorKind.Timeout, true)]
    [InlineData(PlatformErrorKind.Authentication, false)]
    [InlineData(PlatformErrorKind.Forbidden, false)]
    [InlineData(PlatformErrorKind.RateLimited, false)]
    [InlineData(PlatformErrorKind.Contract, false)]
    [InlineData(PlatformErrorKind.Domain, false)]
    [InlineData(PlatformErrorKind.Configuration, false)]
    public void ErrorKindDefinesImmediateRetryPolicy(PlatformErrorKind kind, bool expected)
    {
        Assert.Equal(expected, new PlatformError(kind).CanRetryImmediately);
    }
}
