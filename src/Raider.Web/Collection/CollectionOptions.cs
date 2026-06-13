// 플랫폼 수집 워커의 실행 주기와 제한 시간을 설정한다.
namespace Raider.Web.Collection;

public sealed class CollectionOptions
{
    public bool Enabled { get; init; } = true;

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(10);

    public TimeSpan CollectionTimeout { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan RetryMinimumDelay { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan RetryMaximumDelay { get; init; } = TimeSpan.FromSeconds(3);
}
