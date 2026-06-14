// 공용 즐겨찾기 조회와 변경 HTTP 경계를 등록한다.
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
        if (!await IsValidAntiForgeryRequestAsync(context, antiforgery))
        {
            return Results.BadRequest();
        }

        if (!FavoriteStore.TryParsePlatform(platform, out var parsedPlatform) || !IsValidChannelId(channelId))
        {
            return Results.BadRequest();
        }

        var stream = catalog.FindCurrent(parsedPlatform, channelId);
        if (stream is null)
        {
            return Results.NotFound();
        }

        try
        {
            await store.UpsertAsync(new Favorite(stream.Platform, stream.ChannelId, stream.StreamerName), cancellationToken);
            return Results.NoContent();
        }
        catch (Exception exception) when (IsStoreFailure(exception))
        {
            logger.LogError(exception, "Favorite update failed.");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
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
        if (!await IsValidAntiForgeryRequestAsync(context, antiforgery))
        {
            return Results.BadRequest();
        }

        if (!FavoriteStore.TryParsePlatform(platform, out var parsedPlatform) || !IsValidChannelId(channelId))
        {
            return Results.BadRequest();
        }

        try
        {
            await store.DeleteAsync(parsedPlatform, channelId, cancellationToken);
            return Results.NoContent();
        }
        catch (Exception exception) when (IsStoreFailure(exception))
        {
            logger.LogError(exception, "Favorite delete failed.");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool IsValidChannelId(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length <= 256;
    }

    private static async Task<bool> IsValidAntiForgeryRequestAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static bool IsStoreFailure(Exception exception)
    {
        return exception is SqliteException or IOException or UnauthorizedAccessException;
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

        if (!await IsValidAntiForgeryRequestAsync(context, antiforgery))
        {
            return Results.BadRequest();
        }

        if (!FavoriteStore.TryParsePlatform(platform, out var parsedPlatform) || !IsValidChannelId(channelId))
        {
            return Results.BadRequest();
        }

        try
        {
            await store.UpdateCategoryAsync(parsedPlatform, channelId, request.Category, cancellationToken);
            return Results.NoContent();
        }
        catch (Exception exception) when (IsStoreFailure(exception))
        {
            logger.LogError(exception, "Favorite category update failed.");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}

public sealed record CategoryUpdateRequest(string Category);
