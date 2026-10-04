// 최근 경고 버퍼가 새 항목을 앞에 두고 정해진 개수만 남기는지 검증한다.
using Raider.Web.Status;

namespace Raider.Web.Tests.Status;

public sealed class StatusLogTests
{
    [Fact]
    public void KeepsTheNewestEntriesUpToCapacity()
    {
        var log = new StatusLog();
        var start = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        for (var index = 0; index < StatusLog.Capacity + 5; index++)
        {
            log.Add(start.AddSeconds(index), "SOOP", "collect", "Timeout");
        }

        var recent = log.Recent();
        Assert.Equal(StatusLog.Capacity, recent.Count);
        Assert.Equal(start.AddSeconds(StatusLog.Capacity + 4), recent[0].At);
        Assert.Equal(start.AddSeconds(5), recent[^1].At);
        Assert.Equal("collect", recent[0].Operation);
    }
}
