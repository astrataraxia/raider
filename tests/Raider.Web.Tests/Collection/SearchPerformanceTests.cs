// 대표 라이브 스냅샷에서 검색과 태그 필터의 p95 성능 목표를 검증한다.
using System.Diagnostics;
using Raider.Web.Live;

namespace Raider.Web.Tests.Collection;

public sealed class SearchPerformanceTests
{
    [Fact]
    public void SearchAndTagFilterP95StayBelowOneHundredMilliseconds()
    {
        var streams = Enumerable.Range(0, 5_000)
            .Select(index => LiveStream.Create(
                index % 2 == 0 ? Platform.Chzzk : Platform.Soop,
                $"broadcast-{index}",
                $"channel-{index}",
                $"streamer-{index}",
                $"title-{index}",
                index,
                null,
                $"https://example.invalid/{index}",
                [$"tag-{index % 100}", "common"],
                DateTimeOffset.UtcNow));
        var snapshot = LiveSnapshot.Create(streams, DateTimeOffset.UtcNow);
        var durations = new List<double>();

        for (var iteration = 0; iteration < 100; iteration++)
        {
            var started = Stopwatch.GetTimestamp();
            var result = snapshot.Search(Platform.Chzzk, "common", "title");
            durations.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Assert.NotEmpty(result);
        }

        durations.Sort();
        var p95 = durations[94];
        Console.WriteLine($"snapshot-search-p95-ms={p95:F3}");

        Assert.True(p95 < 100, $"Expected search p95 below 100ms, measured {p95:F2}ms.");
    }
}
