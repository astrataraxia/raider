using System.Collections.Immutable;
using Raider.Web.Live;

namespace Raider.Web.Collection;

public interface ILiveSource
{
    Platform Platform { get; }

    Task<ImmutableArray<LiveStream>> CollectAsync(
        Func<ImmutableArray<LiveStream>, ValueTask>? publishPartial,
        CancellationToken cancellationToken);

    Task<ImmutableArray<LiveStream>> CollectAsync(CancellationToken cancellationToken)
        => CollectAsync(null, cancellationToken);
}
