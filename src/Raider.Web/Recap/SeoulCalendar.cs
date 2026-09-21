// 채팅 집계 날짜는 서버 타임존과 무관하게 Asia/Seoul 역일이다.
namespace Raider.Web.Recap;

public static class SeoulCalendar
{
    private static readonly TimeZoneInfo Zone = Resolve();

    public static DateOnly DateFrom(DateTimeOffset instant)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    private static TimeZoneInfo Resolve()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        }
    }
}
