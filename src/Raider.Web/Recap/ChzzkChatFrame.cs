// 치지직 채팅 소켓 JSON에서 보낸 사람과 시각만 읽는다. 본문은 버린다.
using System.Collections.Immutable;
using System.Text.Json;

namespace Raider.Web.Recap;

public sealed record ChatSender(string SenderChannelId, long MessageTimeMs)
{
    public override string ToString() => $"{SenderChannelId}@{MessageTimeMs}";
}

public static class ChzzkChatFrame
{
    private const int ChatCommand = 93101;

    public static ImmutableArray<ChatSender> Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("cmd", out var command) || command.GetInt32() != ChatCommand)
        {
            return [];
        }

        if (!root.TryGetProperty("bdy", out var body))
        {
            return [];
        }

        var senders = ImmutableArray.CreateBuilder<ChatSender>();
        if (body.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in body.EnumerateArray())
            {
                if (TryReadSender(item, out var sender))
                {
                    senders.Add(sender);
                }
            }
        }
        else if (body.ValueKind == JsonValueKind.Object && TryReadSender(body, out var sender))
        {
            senders.Add(sender);
        }

        return senders.ToImmutable();
    }

    private static bool TryReadSender(JsonElement item, out ChatSender sender)
    {
        sender = null!;
        var id = ReadUid(item);
        if (string.IsNullOrWhiteSpace(id) || string.Equals(id, "anonymous", StringComparison.OrdinalIgnoreCase))
        {
            id = ReadProfileUserId(item);
        }

        if (string.IsNullOrWhiteSpace(id) || string.Equals(id, "anonymous", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!item.TryGetProperty("msgTime", out var time) || time.ValueKind is not JsonValueKind.Number)
        {
            return false;
        }

        sender = new ChatSender(id, time.GetInt64());
        return true;
    }

    private static string? ReadUid(JsonElement item)
    {
        return item.TryGetProperty("uid", out var uid) && uid.ValueKind == JsonValueKind.String
            ? uid.GetString()
            : null;
    }

    private static string? ReadProfileUserId(JsonElement item)
    {
        if (!item.TryGetProperty("profile", out var profile))
        {
            return null;
        }

        if (profile.ValueKind == JsonValueKind.String)
        {
            var raw = profile.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            using var parsed = JsonDocument.Parse(raw);
            return parsed.RootElement.TryGetProperty("userIdHash", out var hash) ? hash.GetString() : null;
        }

        if (profile.ValueKind == JsonValueKind.Object
            && profile.TryGetProperty("userIdHash", out var nested))
        {
            return nested.GetString();
        }

        return null;
    }
}
