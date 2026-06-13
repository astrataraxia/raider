// 실제 CHZZK 공식 API와 어댑터 호환성을 안전한 집계값으로 검증한다.
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Raider.Web.Chzzk;
using Raider.Web.Configuration;

var clientId = Environment.GetEnvironmentVariable("RAIDER_CHZZK_CLIENT_ID");
var clientSecret = Environment.GetEnvironmentVariable("RAIDER_CHZZK_CLIENT_SECRET");
if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
{
    throw new InvalidOperationException("CHZZK smoke test credentials are missing.");
}

using var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://openapi.chzzk.naver.com/"),
    Timeout = TimeSpan.FromSeconds(60),
};
var client = new ChzzkClient(
    httpClient,
    Options.Create(new ChzzkOptions
    {
        ClientId = clientId,
        ClientSecret = clientSecret,
    }),
    TimeProvider.System,
    NullLogger<ChzzkClient>.Instance);

var stopwatch = Stopwatch.StartNew();
var streams = await client.CollectAsync(CancellationToken.None);
stopwatch.Stop();

Console.WriteLine($"streamCount={streams.Length}");
Console.WriteLine($"streamsWithTags={streams.Count(stream => stream.Tags.Length > 0)}");
Console.WriteLine($"elapsedMilliseconds={stopwatch.ElapsedMilliseconds}");
