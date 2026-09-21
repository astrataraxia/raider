// OAuth state를 프로세스 메모리에 둔다. 콜백이 다른 호스트(localhost/127.0.0.1)여도 쿠키 없이 검증한다.
using System.Collections.Concurrent;

namespace Raider.Web.Recap;

public sealed class OauthStateStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> states = new(StringComparer.Ordinal);

    public string Issue()
    {
        var state = Guid.NewGuid().ToString("N");
        states[state] = DateTimeOffset.UtcNow.AddMinutes(10);
        return state;
    }

    public bool Consume(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        return states.TryRemove(state, out var expires) && expires >= DateTimeOffset.UtcNow;
    }
}
