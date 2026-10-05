// 피처드가 라이브 즐겨찾기에서 최대 10개를 섞어 고르는지 검증한다.
using System.Collections.Immutable;
using Raider.Web.Favorites;
using Raider.Web.Live;
using Raider.Web.Pages;

namespace Raider.Web.Tests.Web;

public sealed class FeaturedPickTests
{
    [Fact]
    public void KeepsInputOrderWhenShuffleDoesNotSwap()
    {
        var picks = FeaturedPick.FromSnapshot(Streams(), Favorites(), new StableRandom());

        Assert.Equal(FeaturedPick.MaxCount, picks.Length);
        Assert.Equal(
            Enumerable.Range(0, FeaturedPick.MaxCount).Select(index => $"channel-{index:00}"),
            picks.Select(pick => pick.ChannelId));
    }

    [Fact]
    public void ShuffleChangesWhichTenAreDrawn()
    {
        var picks = FeaturedPick.FromSnapshot(Streams(), Favorites(), new LastIndexRandom());

        Assert.Equal(
            new[] { "channel-11", "channel-00", "channel-01", "channel-02", "channel-03", "channel-04", "channel-05", "channel-06", "channel-07", "channel-08" },
            picks.Select(pick => pick.ChannelId));
    }

    [Fact]
    public void SameSeedDrawsTheSameChannels()
    {
        var first = FeaturedPick.FromSnapshot(Streams(), Favorites(), new Random(7)).Select(pick => pick.ChannelId);
        var second = FeaturedPick.FromSnapshot(Streams(), Favorites(), new Random(7)).Select(pick => pick.ChannelId);
        var changed = Enumerable.Range(2, 20)
            .Any(seed => !first.SequenceEqual(FeaturedPick.FromSnapshot(Streams(), Favorites(), new Random(seed)).Select(pick => pick.ChannelId)));

        Assert.Equal(first, second);
        Assert.True(changed);
    }

    [Fact]
    public void ReturnsEveryLiveFavoriteWhenFewerThanTheCap()
    {
        var streams = Enumerable.Range(0, 3)
            .Select(index => Stream(index, index))
            .ToImmutableArray();
        var favorites = Enumerable.Range(0, 3)
            .Select(index => new Favorite(Platform.Chzzk, $"channel-{index:00}", $"Streamer {index}"))
            .ToImmutableArray();

        var picks = FeaturedPick.FromSnapshot(streams, favorites, new StableRandom());

        Assert.Equal(new[] { "channel-00", "channel-01", "channel-02" }, picks.Select(pick => pick.ChannelId));
    }

    [Fact]
    public void ReturnsEmptyWithoutStreamsOrFavorites()
    {
        Assert.Empty(FeaturedPick.FromSnapshot([], Favorites(), new StableRandom()));
        Assert.Empty(FeaturedPick.FromSnapshot(Streams(), [], new StableRandom()));
    }

    private static ImmutableArray<LiveStream> Streams()
    {
        var favorites = Enumerable.Range(0, 12).Select(index => Stream(index, index));
        return [Stream(99, 100_000, "outsider"), .. favorites];
    }

    private static ImmutableArray<Favorite> Favorites()
    {
        return Enumerable.Range(0, 12)
            .Select(index => new Favorite(Platform.Chzzk, $"channel-{index:00}", $"Streamer {index}"))
            .ToImmutableArray();
    }

    private static LiveStream Stream(int index, int viewers, string? id = null)
    {
        var key = id ?? $"{index:00}";
        return LiveStream.Create(
            Platform.Chzzk,
            key,
            $"channel-{key}",
            $"Streamer {key}",
            "Live",
            viewers,
            "https://example.invalid/thumb.jpg",
            $"https://example.invalid/{key}",
            [],
            DateTimeOffset.UtcNow);
    }

    private sealed class StableRandom : Random
    {
        public override int Next(int minValue, int maxValue) => minValue;
    }

    private sealed class LastIndexRandom : Random
    {
        public override int Next(int minValue, int maxValue) => maxValue - 1;
    }
}
