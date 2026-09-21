// 치지직 OAuth 시작, 콜백, 로그아웃.
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;
using Raider.Web.Configuration;

namespace Raider.Web.Recap;

public static class ChzzkAuthEndpoints
{
    public const string StateCookie = "raider-oauth-state";

    public static void MapChzzkAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/auth/chzzk", Start);
        app.MapGet("/auth/chzzk/callback", CallbackAsync);
        app.MapPost("/auth/logout", LogoutAsync);
    }

    private static IResult Start(
        HttpContext context,
        ChzzkAuthClient auth,
        OauthStateStore states,
        IOptions<ChzzkOptions> options)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return Results.Redirect("/recap");
        }

        if (!options.Value.CanLogin)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var state = states.Issue();
        context.Response.Cookies.Append(
            StateCookie,
            state,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10),
                IsEssential = true,
            });
        return Results.Redirect(auth.CreateAuthorizationUrl(state));
    }

    private static async Task<IResult> CallbackAsync(
        HttpContext context,
        string? code,
        string? state,
        ChzzkAuthClient auth,
        OauthStateStore states,
        CancellationToken cancellationToken)
    {
        context.Response.Cookies.Delete(StateCookie);
        if (string.IsNullOrWhiteSpace(code) || !states.Consume(state))
        {
            return Results.Redirect("/recap?login=failed");
        }

        var user = await auth.ExchangeAsync(code, state!, cancellationToken);
        if (user is null)
        {
            return Results.Redirect("/recap?login=failed");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.ChannelId),
                new Claim(ClaimTypes.Name, user.ChannelName),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
        return Results.Redirect("/recap");
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest();
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/");
    }
}
