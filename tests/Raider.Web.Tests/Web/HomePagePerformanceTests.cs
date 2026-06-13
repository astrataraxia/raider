// 대표 방송 목록의 홈 화면 응답 성능과 HTML 크기 계약을 검증한다.
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Raider.Web.Collection;
using Raider.Web.Live;

namespace Raider.Web.Tests.Web;

public sealed class HomePagePerformanceTests
{
    [Fact]
    public async Task RepresentativeHomePageP95StaysBelowOneHundredMilliseconds()
    {
        await using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration(configuration =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Raider:Collection:Chzzk:Enabled"] = "false",
                        ["Raider:Collection:Soop:Enabled"] = "false",
                    });
                });
            });
        using var client = application.CreateClient();
        var snapshots = application.Services.GetRequiredService<SnapshotStore>();
        var streams = Enumerable.Range(0, 500)
            .Select(index => LiveStream.Create(
                index % 2 == 0 ? Platform.Chzzk : Platform.Soop,
                $"broadcast-{index}",
                $"channel-{index}",
                $"streamer-{index}",
                $"title-{index}",
                index,
                $"https://example.invalid/{index}.jpg",
                $"https://example.invalid/{index}",
                [$"tag-{index % 20}", "common"],
                DateTimeOffset.UtcNow))
            .ToArray();
        snapshots.ApplySuccess(Platform.Chzzk, streams.Where(stream => stream.Platform == Platform.Chzzk).ToImmutableArray(), DateTimeOffset.UtcNow);
        snapshots.ApplySuccess(Platform.Soop, streams.Where(stream => stream.Platform == Platform.Soop).ToImmutableArray(), DateTimeOffset.UtcNow.AddTicks(1));

        _ = await client.GetStringAsync("/", CancellationToken.None);
        var durations = new List<double>();
        string html = string.Empty;
        for (var iteration = 0; iteration < 20; iteration++)
        {
            var started = Stopwatch.GetTimestamp();
            html = await client.GetStringAsync("/?platform=chzzk&tag=common&q=title", CancellationToken.None);
            durations.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        durations.Sort();
        var p95 = durations[18];
        Console.WriteLine($"home-page-p95-ms={p95:F3}; html-bytes={System.Text.Encoding.UTF8.GetByteCount(html)}");

        Assert.True(p95 < 100, $"Expected home page p95 below 100ms, measured {p95:F2}ms.");
        Assert.DoesNotContain("\"broadcastId\":", html, StringComparison.Ordinal);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(html) < 1_000_000, "Filtered home HTML should stay below 1 MB.");
    }
}
