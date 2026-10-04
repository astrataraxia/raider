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
using Raider.Web.Status;

namespace Raider.Web.Recap;

public sealed class ChzzkChatWorker(
    ChatCountStore store,
    FavoriteStore favorites,
    SnapshotStore snapshots,
    ChzzkChatAccess access,
    TimeProvider timeProvider,
    IOptions<ChatOptions> options,
    ILogger<ChzzkChatWorker> logger,
    StatusLog? statusLog = null) : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(15);
    private readonly bool chatEnabled = options.Value.Enabled;
    private readonly object pendingLock = new();
    private readonly object activityLock = new();
    private readonly Dictionary<PendingChat, int> pending = [];
    private readonly ConcurrentDictionary<string, ChatSocket> sockets = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> connected = new(StringComparer.Ordinal);
    private int wantedChannels;
    private DateTimeOffset? lastTickAt;
    private bool lastTickFailed;
    private DateTimeOffset? lastFlushAt;
    private bool? lastFlushSucceeded;

    public ChatActivity ReadActivity()
    {
        var pendingCount = PendingCount();
        var connectedCount = connected.Count;
        lock (activityLock)
        {
            return new ChatActivity(
                chatEnabled,
                wantedChannels,
                connectedCount,
                pendingCount,
                lastTickAt,
                lastTickFailed,
                lastFlushAt,
                lastFlushSucceeded);
        }
    }

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
                lock (activityLock)
                {
                    lastTickAt = timeProvider.GetUtcNow();
                    lastTickFailed = false;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                lock (activityLock)
                {
                    lastTickAt = timeProvider.GetUtcNow();
                    lastTickFailed = true;
                }

                logger.LogWarning(
                    exception,
                    "CHZZK chat collection tick failed. Platform: {Platform}, Operation: {Operation}, ErrorKind: {ErrorKind}",
                    Platform.Chzzk,
                    "chat-tick",
                    "Transient");
                statusLog?.Add(timeProvider.GetUtcNow(), "CHZZK", "chat-tick", "Transient");
            }

            try
            {
                await Task.Delay(FlushInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await FlushPendingChatsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "CHZZK chat count flush failed. Platform: {Platform}, Operation: {Operation}, ErrorKind: {ErrorKind}",
                    Platform.Chzzk,
                    "chat-flush",
                    "Transient");
                statusLog?.Add(timeProvider.GetUtcNow(), "CHZZK", "chat-flush", "Transient");
            }
        }

        await StopSocketsAndFlushAsync();
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

    public Task IngestFrameAsync(string channelId, string json, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        var day = SeoulCalendar.DateFrom(timeProvider.GetUtcNow());
        var senders = ChzzkChatFrame.Read(json);
        lock (pendingLock)
        {
            foreach (var sender in senders)
            {
                var chat = new PendingChat(channelId, sender.SenderChannelId, day);
                pending.TryGetValue(chat, out var count);
                pending[chat] = count + 1;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task FlushPendingChatsAsync(CancellationToken cancellationToken)
    {
        ChatCountDelta[] batch;
        lock (pendingLock)
        {
            if (pending.Count == 0)
            {
                return;
            }

            batch = new ChatCountDelta[pending.Count];
            var index = 0;
            foreach (var (chat, count) in pending)
            {
                batch[index++] = new ChatCountDelta(chat.ChannelId, chat.SenderChannelId, chat.Date, count);
            }

            pending.Clear();
        }

        try
        {
            await store.AddChatCountsAsync(batch, cancellationToken);
        }
        catch (Exception)
        {
            lock (pendingLock)
            {
                foreach (var delta in batch)
                {
                    var chat = new PendingChat(delta.ChannelId, delta.SenderChannelId, delta.Date);
                    pending.TryGetValue(chat, out var count);
                    pending[chat] = count + delta.Count;
                }
            }

            lock (activityLock)
            {
                lastFlushAt = timeProvider.GetUtcNow();
                lastFlushSucceeded = false;
            }

            throw;
        }

        lock (activityLock)
        {
            lastFlushAt = timeProvider.GetUtcNow();
            lastFlushSucceeded = true;
        }
    }

    private int PendingCount()
    {
        lock (pendingLock)
        {
            var total = 0;
            foreach (var count in pending.Values)
            {
                total += count;
            }

            return total;
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
                var reader = ReadChannelAsync(id, linked.Token);
                return new ChatSocket(linked, reader);
            });
        }

        foreach (var channelId in sockets.Keys)
        {
            if (wanted.Contains(channelId))
            {
                continue;
            }

            if (sockets.TryRemove(channelId, out var socket))
            {
                socket.Source.Cancel();
                socket.Source.Dispose();
            }
        }

        lock (activityLock)
        {
            wantedChannels = wanted.Count;
        }
    }

    private async Task StopSocketsAndFlushAsync()
    {
        var running = sockets.Values.ToArray();
        foreach (var socket in running)
        {
            socket.Source.Cancel();
        }

        try
        {
            await Task.WhenAll(running.Select(socket => socket.Reader));
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "CHZZK chat socket stop failed. Platform: {Platform}, Operation: {Operation}, ErrorKind: {ErrorKind}",
                Platform.Chzzk,
                "chat-stop",
                "Transient");
            statusLog?.Add(timeProvider.GetUtcNow(), "CHZZK", "chat-stop", "Transient");
        }

        foreach (var socket in running)
        {
            socket.Source.Dispose();
        }

        sockets.Clear();
        try
        {
            await FlushPendingChatsAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "CHZZK chat count flush failed. Platform: {Platform}, Operation: {Operation}, ErrorKind: {ErrorKind}",
                Platform.Chzzk,
                "chat-flush",
                "Transient");
            statusLog?.Add(timeProvider.GetUtcNow(), "CHZZK", "chat-flush", "Transient");
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
                connected[channelId] = 0;
                try
                {
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
                finally
                {
                    connected.TryRemove(channelId, out _);
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
                statusLog?.Add(timeProvider.GetUtcNow(), "CHZZK", "chat-socket", "Transient");
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

    public readonly record struct ChatActivity(
        bool Enabled,
        int Wanted,
        int Connected,
        int PendingCount,
        DateTimeOffset? LastTickAt,
        bool LastTickFailed,
        DateTimeOffset? LastFlushAt,
        bool? LastFlushSucceeded);

    private readonly record struct PendingChat(string ChannelId, string SenderChannelId, DateOnly Date);

    private sealed class ChatSocket(CancellationTokenSource source, Task reader)
    {
        public CancellationTokenSource Source { get; } = source;
        public Task Reader { get; } = reader;
    }
}
