// 플랫폼 수집 실패 종류와 즉시 재시도 가능 여부를 나타낸다.
namespace Raider.Web.Collection;

public sealed record PlatformError(PlatformErrorKind Kind)
{
    public bool CanRetryImmediately => Kind is
        PlatformErrorKind.Network or
        PlatformErrorKind.Server or
        PlatformErrorKind.Timeout;
}

public sealed class PlatformCollectionException : Exception
{
    public PlatformCollectionException(PlatformError error, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public PlatformError Error { get; }
}

public enum PlatformErrorKind
{
    Network,
    Server,
    Timeout,
    Authentication,
    Forbidden,
    RateLimited,
    Contract,
    Domain,
    Configuration,
}
