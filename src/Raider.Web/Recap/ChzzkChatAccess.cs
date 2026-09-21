// 치지직 방송의 채팅방 식별자와 읽기 토큰을 조회한다.
using System.Net.Http.Json;
using System.Text.Json;

namespace Raider.Web.Recap;

public sealed record ChatAccess(string ChannelId, string ChatChannelId, string AccessToken);

public sealed class ChzzkChatAccess(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task<ChatAccess?> TryOpenAsync(string channelId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        using var statusRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.chzzk.naver.com/polling/v2/channels/{Uri.EscapeDataString(channelId)}/live-status");
        using var statusResponse = await httpClient.SendAsync(statusRequest, cancellationToken);
        if (!statusResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var status = await statusResponse.Content.ReadFromJsonAsync<LiveStatusEnvelope>(Json, cancellationToken);
        var chatChannelId = status?.Content?.ChatChannelId;
        if (string.IsNullOrWhiteSpace(chatChannelId)
            || !string.Equals(status?.Content?.Status, "OPEN", StringComparison.Ordinal))
        {
            return null;
        }

        using var tokenRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://comm-api.game.naver.com/nng_main/v1/chats/access-token?channelId={Uri.EscapeDataString(chatChannelId)}&chatType=STREAMING");
        using var tokenResponse = await httpClient.SendAsync(tokenRequest, cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var token = await tokenResponse.Content.ReadFromJsonAsync<AccessTokenEnvelope>(Json, cancellationToken);
        var accessToken = token?.Content?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        return new ChatAccess(channelId, chatChannelId, accessToken);
    }

    private sealed record LiveStatusEnvelope(LiveStatusContent? Content);

    private sealed record LiveStatusContent(string? Status, string? ChatChannelId);

    private sealed record AccessTokenEnvelope(AccessTokenContent? Content);

    private sealed record AccessTokenContent(string? AccessToken);
}
