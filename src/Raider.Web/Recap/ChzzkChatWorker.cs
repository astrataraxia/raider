// 즐겨찾기 CHZZK 라이브의 채팅 건수와 방송일을 모은다. 본문은 저장하지 않는다.
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raider.Web.Collection;
using Raider.Web.Favorites;
using Raider.Web.Live;

namespace Raider.Web.Recap;

public sealed class ChzzkChatWorker(
    ChatCountStore store,
    FavoriteStore favorites,
    SnapshotStore snapshots,
    ChzzkChatAccess access,
    TimeProvider timeProvider,
    IOptions<ChatOptions> options,
    ILogger<ChzzkChatWorker> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> sockets = new(StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ObserveLiveFavoritesAsync(stoppingToken);
                await SyncSocketsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "CHZZK chat collection tick failed. Platform: {Platform}, Operation: {Operation}, ErrorKind: {ErrorKind}",
                    Platform.Chzzk,
                    "chat-tick",
                    "Transient");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        foreach (var source in sockets.Values)
        {
            source.Cancel();
            source.Dispose();
        }

        sockets.Clear();
    }

    public async Task ObserveLiveFavoritesAsync(CancellationToken cancellationToken)
    {
        var wanted = await LiveFavoriteChannelIdsAsync(cancellationToken);
        var day = SeoulCalendar.DateFrom(timeProvider.GetUtcNow());
        foreach (var channelId in wanted)
        {
            await store.RecordBroadcastDayAsync(channelId, day, cancellationToken);
        }
    }

    public async Task IngestFrameAsync(string channelId, string json, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        var day = SeoulCalendar.DateFrom(timeProvider.GetUtcNow());
        foreach (var sender in ChzzkChatFrame.Read(json))
        {
            await store.AddChatAsync(channelId, sender.SenderChannelId, day, cancellationToken);
        }
    }

    private async Task SyncSocketsAsync(CancellationToken cancellationToken)
    {
        var wanted = await LiveFavoriteChannelIdsAsync(cancellationToken);
        foreach (var channelId in wanted)
        {
            sockets.GetOrAdd(channelId, id =>
            {
                var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _ = ReadChannelAsync(id, linked.Token);
                return linked;
            });
        }

        foreach (var channelId in sockets.Keys)
        {
            if (wanted.Contains(channelId))
            {
                continue;
            }

            if (sockets.TryRemove(channelId, out var source))
            {
                source.Cancel();
                source.Dispose();
            }
        }
    }

    private async Task<HashSet<string>> LiveFavoriteChannelIdsAsync(CancellationToken cancellationToken)
    {
        var favoriteIds = (await favorites.ListAsync(cancellationToken))
            .Where(favorite => favorite.Platform == Platform.Chzzk)
            .Select(favorite => favorite.ChannelId)
            .ToHashSet(StringComparer.Ordinal);
        return snapshots.Current.Live.Streams
            .Where(stream => stream.Platform == Platform.Chzzk && favoriteIds.Contains(stream.ChannelId))
            .Select(stream => stream.ChannelId)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task ReadChannelAsync(string channelId, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var session = await access.TryOpenAsync(channelId, cancellationToken);
                if (session is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
                    continue;
                }

                using var socket = new ClientWebSocket();
                var server = Math.Abs(session.ChatChannelId.Sum(character => (int)character)) % 9 + 1;
                await socket.ConnectAsync(
                    new Uri($"wss://kr-ss{server}.chat.naver.com/chat"),
                    cancellationToken);
                await socket.SendAsync(
                    Encoding.UTF8.GetBytes(ConnectPayload(session)),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);

                var buffer = new byte[64 * 1024];
                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    // ponytail: one WS frame per JSON message; assemble if CHZZK starts splitting
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.TryGetProperty("cmd", out var command) && command.GetInt32() == 0)
                    {
                        await socket.SendAsync(
                            Encoding.UTF8.GetBytes("""{"cmd":10000,"ver":"2"}"""),
                            WebSocketMessageType.Text,
                            true,
                            cancellationToken);
                        continue;
                    }

                    await IngestFrameAsync(channelId, json, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "CHZZK chat socket failed. Platform: {Platform}, Operation: {Operation}, ErrorKind: {ErrorKind}",
                    Platform.Chzzk,
                    "chat-socket",
                    "Transient");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private static string ConnectPayload(ChatAccess session)
        => JsonSerializer.Serialize(new
        {
            ver = "2",
            cmd = 100,
            svcid = "game",
            cid = session.ChatChannelId,
            tid = 1,
            bdy = new { accTkn = session.AccessToken, auth = "READ", devType = 2001 },
        });
}
