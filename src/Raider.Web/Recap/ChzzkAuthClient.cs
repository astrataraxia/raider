// 치지직 OAuth 코드로 이용자 채널을 확인한다.
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Raider.Web.Configuration;

namespace Raider.Web.Recap;

public sealed record ChzzkUser(string ChannelId, string ChannelName);

public sealed class ChzzkAuthClient(HttpClient httpClient, IOptions<ChzzkOptions> options)
{
    private readonly ChzzkOptions options = options.Value;

    public string CreateAuthorizationUrl(string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        if (!this.options.CanLogin)
        {
            throw new InvalidOperationException("CHZZK login redirect is not configured.");
        }

        return "https://chzzk.naver.com/account-interlock"
            + $"?clientId={Uri.EscapeDataString(this.options.ClientId)}"
            + $"&redirectUri={Uri.EscapeDataString(this.options.RedirectUri)}"
            + $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task<ChzzkUser?> ExchangeAsync(string code, string state, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        try
        {
            return await ExchangeCoreAsync(code, state, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<ChzzkUser?> ExchangeCoreAsync(string code, string state, CancellationToken cancellationToken)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "auth/v1/token")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    grantType = "authorization_code",
                    clientId = options.ClientId,
                    clientSecret = options.ClientSecret,
                    code,
                    state,
                    redirectUri = options.RedirectUri,
                }),
                Encoding.UTF8,
                "application/json"),
        };
        tokenRequest.Headers.Add("Client-Id", options.ClientId);
        tokenRequest.Headers.Add("Client-Secret", options.ClientSecret);
        using var tokenResponse = await httpClient.SendAsync(tokenRequest, cancellationToken);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var tokenDocument = JsonDocument.Parse(tokenJson);
        var accessToken = ReadString(tokenDocument.RootElement, "accessToken");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "open/v1/users/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var meResponse = await httpClient.SendAsync(meRequest, cancellationToken);
        if (!meResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var meDocument = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync(cancellationToken));
        var channelId = ReadString(meDocument.RootElement, "channelId");
        var channelName = ReadString(meDocument.RootElement, "channelName")
            ?? ReadString(meDocument.RootElement, "nickname")
            ?? ReadString(meDocument.RootElement, "name");
        if (string.IsNullOrWhiteSpace(channelId) || string.IsNullOrWhiteSpace(channelName))
        {
            return null;
        }

        return new ChzzkUser(channelId, channelName);
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (TryGetPropertyIgnoreCase(root, name, out var direct) && direct.ValueKind == JsonValueKind.String)
        {
            return direct.GetString();
        }

        if (TryGetPropertyIgnoreCase(root, "content", out var content)
            && TryGetPropertyIgnoreCase(content, name, out var nested)
            && nested.ValueKind == JsonValueKind.String)
        {
            return nested.GetString();
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
