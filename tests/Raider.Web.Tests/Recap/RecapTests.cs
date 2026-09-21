// 본진, 뱃지, 연속 출석 집계 계약을 검증한다.
using System.Collections.Immutable;
using Raider.Web.Recap;

namespace Raider.Web.Tests.Recap;

public sealed class RecapTests
{
    [Fact]
    public void HomeShareUsesMostChattedChannel()
    {
        var recap = ViewerRecap.ForViewer(
            "me",
            [
                Day("home", "me", 2026, 9, 1, 80),
                Day("other", "me", 2026, 9, 1, 20),
            ],
            [],
            [],
            Names());

        Assert.Equal("home", recap.HomeChannelId);
        Assert.Equal("Home", recap.HomeChannelName);
        Assert.Equal(0.8, recap.HomeShare);
        Assert.Equal(100, recap.TotalChats);
        Assert.Equal(2, recap.ActiveChannels);
        Assert.Equal(1, recap.ActiveDays);
    }

    [Fact]
    public void AttendanceBadgeRequiresHalfOfHomeBroadcastDays()
    {
        var days = ImmutableArray.Create(
            Day("home", "me", 2026, 9, 1, 1),
            Day("home", "me", 2026, 9, 2, 1));
        var broadcasts = ImmutableArray.Create(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 2),
            new DateOnly(2026, 9, 3),
            new DateOnly(2026, 9, 4));

        var passing = ViewerRecap.ForViewer("me", days, broadcasts, [], Names());
        var failing = ViewerRecap.ForViewer(
            "me",
            [Day("home", "me", 2026, 9, 1, 1)],
            broadcasts,
            [],
            Names());

        Assert.True(passing.HasAttendanceBadge);
        Assert.False(failing.HasAttendanceBadge);
        Assert.Equal(0.5, passing.AttendanceRate);
        Assert.Equal(0.25, failing.AttendanceRate);
    }

    [Fact]
    public void LoyaltyAndHopperBadgesUseHomeShareAndChannelCount()
    {
        var loyal = ViewerRecap.ForViewer(
            "me",
            [Day("home", "me", 2026, 9, 1, 10)],
            [],
            [],
            Names());
        var hopper = ViewerRecap.ForViewer(
            "me",
            [
                Day("home", "me", 2026, 9, 1, 1),
                Day("a", "me", 2026, 9, 1, 1),
                Day("b", "me", 2026, 9, 1, 1),
            ],
            [],
            [],
            Names(("a", "A"), ("b", "B")));

        Assert.True(loyal.HasLoyaltyBadge);
        Assert.False(loyal.HasHopperBadge);
        Assert.False(hopper.HasLoyaltyBadge);
        Assert.True(hopper.HasHopperBadge);
    }

    [Fact]
    public void StreakSkipsDaysWithoutABroadcast()
    {
        var recap = ViewerRecap.ForViewer(
            "me",
            [
                Day("home", "me", 2026, 9, 1, 1),
                Day("home", "me", 2026, 9, 2, 1),
                Day("home", "me", 2026, 9, 4, 1),
            ],
            [
                new DateOnly(2026, 9, 1),
                new DateOnly(2026, 9, 2),
                new DateOnly(2026, 9, 4),
            ],
            [],
            Names());

        Assert.Equal(3, recap.LongestStreak);
        Assert.Equal(3, recap.CurrentStreak);
    }

    [Fact]
    public void MissedBroadcastDayBreaksStreak()
    {
        var recap = ViewerRecap.ForViewer(
            "me",
            [
                Day("home", "me", 2026, 9, 1, 1),
                Day("home", "me", 2026, 9, 4, 1),
            ],
            [
                new DateOnly(2026, 9, 1),
                new DateOnly(2026, 9, 2),
                new DateOnly(2026, 9, 4),
            ],
            [],
            Names());

        Assert.Equal(1, recap.LongestStreak);
        Assert.Equal(1, recap.CurrentStreak);
    }

    [Fact]
    public void VeteranBadgeUsesEarlyFirstSeenOnHomeChannel()
    {
        var firstSeen = ImmutableArray.Create(
            new ChannelFirstSeen("early", new DateOnly(2026, 6, 1)),
            new ChannelFirstSeen("me", new DateOnly(2026, 6, 2)),
            new ChannelFirstSeen("late", new DateOnly(2026, 9, 1)));

        var veteran = ViewerRecap.ForViewer(
            "early",
            [Day("home", "early", 2026, 6, 1, 1)],
            [],
            firstSeen,
            Names());
        var late = ViewerRecap.ForViewer(
            "late",
            [Day("home", "late", 2026, 9, 1, 1)],
            [],
            firstSeen,
            Names());

        Assert.True(veteran.HasVeteranBadge);
        Assert.False(late.HasVeteranBadge);
        Assert.Equal(new DateOnly(2026, 6, 1), veteran.HomeFirstSeen);
    }

    [Fact]
    public void EmptyCountsProduceEmptyRecapWithoutBadges()
    {
        var recap = ViewerRecap.ForViewer("me", [], [], [], Names());

        Assert.Equal(0, recap.TotalChats);
        Assert.Null(recap.HomeChannelId);
        Assert.False(recap.HasAttendanceBadge);
        Assert.False(recap.HasLoyaltyBadge);
        Assert.False(recap.HasHopperBadge);
        Assert.False(recap.HasVeteranBadge);
        Assert.Empty(recap.Channels);
        Assert.Empty(recap.Months);
    }

    [Fact]
    public void MonthlyTotalsGroupByYearMonth()
    {
        var recap = ViewerRecap.ForViewer(
            "me",
            [
                Day("home", "me", 2026, 7, 1, 10),
                Day("home", "me", 2026, 7, 20, 5),
                Day("home", "me", 2026, 8, 1, 3),
            ],
            [],
            [],
            Names());

        Assert.Equal(2, recap.Months.Length);
        Assert.Equal(new DateOnly(2026, 7, 1), recap.Months[0].Month);
        Assert.Equal(15, recap.Months[0].Count);
        Assert.Equal(3, recap.Months[1].Count);
    }

    private static ChatDayCount Day(string channelId, string sender, int year, int month, int day, int count)
        => new(channelId, sender, new DateOnly(year, month, day), count);

    private static IReadOnlyDictionary<string, string> Names(params (string Id, string Name)[] extra)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["home"] = "Home",
            ["other"] = "Other",
        };
        foreach (var (id, name) in extra)
        {
            names[id] = name;
        }

        return names;
    }
}
