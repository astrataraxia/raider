// 한 시청자가 한 채널에서 하루에 보낸 채팅 건수다.
namespace Raider.Web.Recap;

public sealed class ChatDayCount
{
    public string ChannelId { get; set; } = string.Empty;
    public string SenderChannelId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int Count { get; set; }

    public ChatDayCount() { }

    public ChatDayCount(string channelId, string senderChannelId, DateOnly date, int count)
    {
        ChannelId = channelId;
        SenderChannelId = senderChannelId;
        Date = date;
        Count = count;
    }
}

public sealed record ChatCountDelta(string ChannelId, string SenderChannelId, DateOnly Date, int Count);

public sealed record ChannelFirstSeen(string SenderChannelId, DateOnly FirstSeen);

public sealed class BroadcastDay
{
    public string ChannelId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }

    public BroadcastDay() { }

    public BroadcastDay(string channelId, DateOnly date)
    {
        ChannelId = channelId;
        Date = date;
    }
}
