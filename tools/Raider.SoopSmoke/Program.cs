// 실제 SOOP 공식 broad/list 응답의 구조를 비밀값과 원본 방송 정보 없이 검증한다.
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Raider.Web.Soop.SoopClient).Assembly, optional: false)
    .Build();
var clientId = configuration["Raider:Soop:ClientId"];
if (string.IsNullOrWhiteSpace(clientId))
{
    throw new InvalidOperationException("Raider:Soop:ClientId is required in User Secrets.");
}

using var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://openapi.sooplive.com/"),
    Timeout = TimeSpan.FromSeconds(30),
};
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Raider.SoopSmoke/1.0");

var path = $"broad/list?client_id={Uri.EscapeDataString(clientId)}&select_key=cate&order_type=broad_start&page_no=1";

var stopwatch = Stopwatch.StartNew();
using var response = await httpClient.GetAsync(path, CancellationToken.None);
var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
stopwatch.Stop();

using var document = JsonDocument.Parse(body);
var root = document.RootElement;
var result = root.TryGetProperty("result", out var resultElement) ? resultElement.ToString() : "<absent>";
var message = root.TryGetProperty("msg", out var messageElement) ? messageElement.ToString() : "<absent>";
var broadcastCount = root.TryGetProperty("broad", out var broadcasts) && broadcasts.ValueKind == JsonValueKind.Array
    ? broadcasts.GetArrayLength()
    : -1;
var fieldNames = root.TryGetProperty("broad", out broadcasts) && broadcasts.ValueKind == JsonValueKind.Array && broadcasts.GetArrayLength() > 0
    ? string.Join(',', broadcasts[0].EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal))
    : "<absent>";

Console.WriteLine($"httpStatus={(int)response.StatusCode}");
Console.WriteLine($"result={result}");
Console.WriteLine($"messagePresent={message != "<absent>"}");
Console.WriteLine($"totalCount={GetNumberOrAbsent(root, "total_cnt")}");
Console.WriteLine($"pageNumber={GetNumberOrAbsent(root, "page_no")}");
Console.WriteLine($"pageBlock={GetNumberOrAbsent(root, "page_block")}");
Console.WriteLine($"broadcastCount={broadcastCount}");
Console.WriteLine($"broadcastFields={fieldNames}");
Console.WriteLine($"elapsedMilliseconds={stopwatch.ElapsedMilliseconds}");

static string GetNumberOrAbsent(JsonElement root, string propertyName)
{
    return root.TryGetProperty(propertyName, out var value) ? value.ToString() : "<absent>";
}
