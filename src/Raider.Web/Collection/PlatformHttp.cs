using System.Net;

namespace Raider.Web.Collection;

internal static class PlatformHttp
{
    public static PlatformCollectionException HttpError(string platform, HttpStatusCode statusCode)
    {
        var kind = statusCode switch
        {
            HttpStatusCode.Unauthorized => PlatformErrorKind.Authentication,
            HttpStatusCode.Forbidden => PlatformErrorKind.Forbidden,
            HttpStatusCode.RequestTimeout => PlatformErrorKind.Timeout,
            HttpStatusCode.TooManyRequests => PlatformErrorKind.RateLimited,
            >= HttpStatusCode.InternalServerError => PlatformErrorKind.Server,
            _ => PlatformErrorKind.Contract,
        };

        return new PlatformCollectionException(new PlatformError(kind), $"{platform} request failed with HTTP {(int)statusCode}.");
    }

    public static PlatformCollectionException RequestFailed(string platform, Exception exception, CancellationToken cancellationToken)
    {
        return exception switch
        {
            OperationCanceledException when !cancellationToken.IsCancellationRequested => new PlatformCollectionException(
                new PlatformError(PlatformErrorKind.Timeout),
                $"{platform} request timed out.",
                exception),
            HttpRequestException => new PlatformCollectionException(
                new PlatformError(PlatformErrorKind.Network),
                $"{platform} network request failed.",
                exception),
            System.Text.Json.JsonException => new PlatformCollectionException(
                new PlatformError(PlatformErrorKind.Contract),
                $"{platform} response contract was invalid.",
                exception),
            _ => throw exception,
        };
    }
}
