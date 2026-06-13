// 실제 SOOP 공개 웹 JSON과 어댑터 호환성을 안전한 집계값으로 검증한다.
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Raider.Web.Soop;

using var handler = new HttpClientHandler
{
    UseCookies = false,
};
using var httpClient = new HttpClient(handler)
{
    BaseAddress = new Uri("https://live.sooplive.com/"),
    Timeout = TimeSpan.FromSeconds(30),
};
var client = new SoopClient(httpClient, TimeProvider.System, NullLogger<SoopClient>.Instance);

var stopwatch = Stopwatch.StartNew();
var streams = await client.CollectAsync(CancellationToken.None);
stopwatch.Stop();

Console.WriteLine($"streamCount={streams.Length}");
Console.WriteLine($"streamsWithTags={streams.Count(stream => stream.Tags.Length > 0)}");
Console.WriteLine($"elapsedMilliseconds={stopwatch.ElapsedMilliseconds}");
