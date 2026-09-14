using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;
using Raider.Web.Live;

namespace Raider.Web.Favorites;

public static class FavoriteEndpoints
{
    public static void MapFavoriteEndpoints(this WebApplication app)
    {
        app.MapGet("/api/favorites", GetAsync);
        app.MapPut("/api/favorites/{platform}/{channelId}", PutAsync);
        app.MapDelete("/api/favorites/{platform}/{channelId}", DeleteAsync);
        app.MapPut("/api/favorites/{platform}/{channelId}/category", UpdateCategoryAsync);
    }

    private static async Task<IResult> GetAsync(
        FavoriteCatalog catalog,
        ILogger<FavoriteStore> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Json(await catalog.ListAsync(cancellationToken));
        }
        catch (Exception exception) when (IsStoreFailure(exception))
        {
            logger.LogError(exception, "Favorite list failed.");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> PutAsync(
        string platform,
        string channelId,
        HttpContext context,
        IAntiforgery antiforgery,
        FavoriteCatalog catalog,
        FavoriteStore store,
        ILogger<FavoriteStore> logger,
        CancellationToken cancellationToken)
    {
        var guard = await GuardWriteAsync(platform, channelId, context, antiforgery);
        if (guard.Error is not null)
        {
            return guard.Error;
        }

        var stream = catalog.FindCurrent(guard.Platform, channelId);
        if (stream is null)
        {
            return Results.NotFound();
        }

        return await TryStoreAsync(
            () => store.UpsertAsync(new Favorite(stream.Platform, stream.ChannelId, stream.StreamerName), cancellationToken),
            logger,
            "Favorite update failed.");
    }

    private static async Task<IResult> DeleteAsync(
        string platform,
        string channelId,
        HttpContext context,
        IAntiforgery antiforgery,
        FavoriteStore store,
        ILogger<FavoriteStore> logger,
        CancellationToken cancellationToken)
    {
        var guard = await GuardWriteAsync(platform, channelId, context, antiforgery);
        if (guard.Error is not null)
        {
            return guard.Error;
        }

        return await TryStoreAsync(
            () => store.DeleteAsync(guard.Platform, channelId, cancellationToken),
            logger,
            "Favorite delete failed.");
    }

    private static async Task<IResult> UpdateCategoryAsync(
        string platform,
        string channelId,
        CategoryUpdateRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        FavoriteStore store,
        ILogger<FavoriteStore> logger,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Category))
        {
            return Results.BadRequest();
        }

        var guard = await GuardWriteAsync(platform, channelId, context, antiforgery);
        if (guard.Error is not null)
        {
            return guard.Error;
        }

        return await TryStoreAsync(
            () => store.UpdateCategoryAsync(guard.Platform, channelId, request.Category, cancellationToken),
            logger,
            "Favorite category update failed.");
    }

    private static async Task<(Platform Platform, IResult? Error)> GuardWriteAsync(
        string platform,
        string channelId,
        HttpContext context,
        IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return (default, Results.BadRequest());
        }

        if (!FavoriteStore.TryParsePlatform(platform, out var parsedPlatform)
            || string.IsNullOrWhiteSpace(channelId)
            || channelId.Length > 256)
        {
            return (default, Results.BadRequest());
        }

        return (parsedPlatform, null);
    }

    private static async Task<IResult> TryStoreAsync(Func<Task> action, ILogger logger, string failureMessage)
    {
        try
        {
            await action();
            return Results.NoContent();
        }
        catch (Exception exception) when (IsStoreFailure(exception))
        {
            logger.LogError(exception, failureMessage);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool IsStoreFailure(Exception exception)
    {
        return exception is SqliteException or IOException or UnauthorizedAccessException;
    }
}

public sealed record CategoryUpdateRequest(string Category);
