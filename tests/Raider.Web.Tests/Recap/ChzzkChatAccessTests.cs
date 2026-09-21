// 치지직 채팅방 식별자와 읽기 토큰 조회 계약을 검증한다.
using System.Net;
using Raider.Web.Recap;

namespace Raider.Web.Tests.Recap;

public sealed class ChzzkChatAccessTests
{
    [Fact]
    public async Task OpenLiveChannelReturnsChatAccess()
    {
        var handler = new FixtureHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("live-status", StringComparison.Ordinal))
            {
                return Json("""{"code":200,"content":{"status":"OPEN","chatChannelId":"chat-room-1"}}""");
            }

            Assert.Contains("chat-room-1", request.RequestUri.Query, StringComparison.Ordinal);
            return Json("""{"code":0,"content":{"accessToken":"read-token"}}""");
        });

        var access = await new ChzzkChatAccess(new HttpClient(handler)).TryOpenAsync("channel-1", CancellationToken.None);

        Assert.NotNull(access);
        Assert.Equal("channel-1", access.ChannelId);
        Assert.Equal("chat-room-1", access.ChatChannelId);
        Assert.Equal("read-token", access.AccessToken);
    }

    [Fact]
    public async Task ClosedOrMissingChatChannelReturnsNull()
    {
        var closed = new FixtureHandler(_ => Json("""{"code":200,"content":{"status":"CLOSE","chatChannelId":"x"}}"""));
        var missing = new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Null(await new ChzzkChatAccess(new HttpClient(closed)).TryOpenAsync("channel-1", CancellationToken.None));
        Assert.Null(await new ChzzkChatAccess(new HttpClient(missing)).TryOpenAsync("channel-1", CancellationToken.None));
    }

    private static HttpResponseMessage Json(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private sealed class FixtureHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
