// 한 시청자의 CHZZK 채팅 날짜 건수로 리캡을 만든다. 피라미드는 계산하지 않는다.
using System.Collections.Immutable;

namespace Raider.Web.Recap;

public sealed record RecapMonth(DateOnly Month, int Count);

public sealed record RecapChannel(string ChannelId, string Name, int Count);

public sealed record ViewerRecap(
    string? HomeChannelId,
    string? HomeChannelName,
    double HomeShare,
    int TotalChats,
    int ActiveChannels,
    int ActiveDays,
    double AttendanceRate,
    bool HasAttendanceBadge,
    bool HasLoyaltyBadge,
    bool HasHopperBadge,
    bool HasVeteranBadge,
    DateOnly? HomeFirstSeen,
    int LongestStreak,
    int CurrentStreak,
    ImmutableArray<RecapChannel> Channels,
    ImmutableArray<RecapMonth> Months)
{
    public const double AttendanceThreshold = 0.5;
    public const double LoyaltyThreshold = 0.5;
    public const int HopperChannelThreshold = 3;

    public static ViewerRecap Empty { get; } = ForViewer("-", [], [], [], new Dictionary<string, string>());

    public static ViewerRecap ForViewer(
        string viewerChannelId,
        ImmutableArray<ChatDayCount> viewerDays,
        ImmutableArray<DateOnly> homeBroadcastDays,
        ImmutableArray<ChannelFirstSeen> homeFirstSeen,
        IReadOnlyDictionary<string, string> channelNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerChannelId);
        ArgumentNullException.ThrowIfNull(channelNames);

        if (viewerDays.IsDefaultOrEmpty)
        {
            return new ViewerRecap(
                null,
                null,
                0,
                0,
                0,
                0,
                0,
                false,
                false,
                false,
                false,
                null,
                0,
                0,
                [],
                []);
        }

        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        var dates = new HashSet<DateOnly>();
        var months = new Dictionary<DateOnly, int>();
        var total = 0;
        foreach (var row in viewerDays)
        {
            totals[row.ChannelId] = totals.GetValueOrDefault(row.ChannelId) + row.Count;
            dates.Add(row.Date);
            var month = new DateOnly(row.Date.Year, row.Date.Month, 1);
            months[month] = months.GetValueOrDefault(month) + row.Count;
            total += row.Count;
        }

        var home = totals.OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .First();
        var homeShare = total == 0 ? 0 : (double)home.Value / total;
        var homeName = channelNames.GetValueOrDefault(home.Key, home.Key);
        var chattedHomeDays = viewerDays
            .Where(row => row.ChannelId == home.Key)
            .Select(row => row.Date)
            .ToHashSet();
        var attendanceRate = homeBroadcastDays.IsDefaultOrEmpty
            ? 0
            : (double)homeBroadcastDays.Count(chattedHomeDays.Contains) / homeBroadcastDays.Length;
        var (longest, current) = Streaks(homeBroadcastDays, chattedHomeDays);
        var firstSeen = homeFirstSeen.IsDefaultOrEmpty
            ? null
            : homeFirstSeen.FirstOrDefault(row => row.SenderChannelId == viewerChannelId)?.FirstSeen;
        var veteran = IsVeteran(viewerChannelId, homeFirstSeen);

        var channels = totals
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new RecapChannel(pair.Key, channelNames.GetValueOrDefault(pair.Key, pair.Key), pair.Value))
            .ToImmutableArray();
        var monthRows = months
            .OrderBy(pair => pair.Key)
            .Select(pair => new RecapMonth(pair.Key, pair.Value))
            .ToImmutableArray();

        return new ViewerRecap(
            home.Key,
            homeName,
            homeShare,
            total,
            totals.Count,
            dates.Count,
            attendanceRate,
            attendanceRate >= AttendanceThreshold && homeBroadcastDays.Length > 0,
            homeShare >= LoyaltyThreshold,
            totals.Count >= HopperChannelThreshold,
            veteran,
            firstSeen,
            longest,
            current,
            channels,
            monthRows);
    }

    private static bool IsVeteran(string viewerChannelId, ImmutableArray<ChannelFirstSeen> homeFirstSeen)
    {
        if (homeFirstSeen.IsDefaultOrEmpty)
        {
            return false;
        }

        var ordered = homeFirstSeen
            .OrderBy(row => row.FirstSeen)
            .ThenBy(row => row.SenderChannelId, StringComparer.Ordinal)
            .ToArray();
        var cutoff = Math.Max(1, (int)Math.Ceiling(ordered.Length * 0.1));
        for (var index = 0; index < cutoff; index++)
        {
            if (ordered[index].SenderChannelId == viewerChannelId)
            {
                return true;
            }
        }

        return false;
    }

    private static (int Longest, int Current) Streaks(ImmutableArray<DateOnly> broadcastDays, HashSet<DateOnly> chattedDays)
    {
        if (broadcastDays.IsDefaultOrEmpty)
        {
            return (0, 0);
        }

        var ordered = broadcastDays.OrderBy(day => day).ToArray();
        var longest = 0;
        var run = 0;
        foreach (var day in ordered)
        {
            if (chattedDays.Contains(day))
            {
                run++;
                if (run > longest)
                {
                    longest = run;
                }
            }
            else
            {
                run = 0;
            }
        }

        var current = 0;
        for (var index = ordered.Length - 1; index >= 0; index--)
        {
            if (!chattedDays.Contains(ordered[index]))
            {
                break;
            }

            current++;
        }

        return (longest, current);
    }
}
