// 한 시청자가 한 채널에서 하루에 보낸 채팅 건수다.
namespace Raider.Web.Recap;

public sealed record ChatDayCount(string ChannelId, string SenderChannelId, DateOnly Date, int Count);

public sealed record ChannelFirstSeen(string SenderChannelId, DateOnly FirstSeen);
