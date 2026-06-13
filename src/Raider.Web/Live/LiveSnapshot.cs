// 정렬된 현재 라이브와 태그 인덱스를 원자 교체 가능한 읽기 모델로 묶는다.
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Raider.Web.Live;

public sealed class LiveSnapshot
{
    private LiveSnapshot(
        ImmutableArray<LiveStream> streams,
        FrozenDictionary<string, ImmutableArray<int>> streamsByTag,
        DateTimeOffset observedAt)
    {
        Streams = streams;
        StreamsByTag = streamsByTag;
        ObservedAt = observedAt;
    }

    public ImmutableArray<LiveStream> Streams { get; }

    public FrozenDictionary<string, ImmutableArray<int>> StreamsByTag { get; }

    public DateTimeOffset ObservedAt { get; }

    public static LiveSnapshot Create(IEnumerable<LiveStream> streams, DateTimeOffset observedAt)
    {
        var ordered = LiveStream.OrderAndDeduplicate(streams);
        var indicesByTag = new Dictionary<string, ImmutableArray<int>.Builder>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < ordered.Length; index++)
        {
            foreach (var tag in ordered[index].Tags)
            {
                var key = LiveStream.NormalizeSearch(tag);
                if (!indicesByTag.TryGetValue(key, out var indices))
                {
                    indices = ImmutableArray.CreateBuilder<int>();
                    indicesByTag.Add(key, indices);
                }

                indices.Add(index);
            }
        }

        var frozenIndex = indicesByTag.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.ToImmutable(),
            StringComparer.OrdinalIgnoreCase);

        return new LiveSnapshot(ordered, frozenIndex, observedAt);
    }

    public ImmutableArray<LiveStream> Search(
        Platform? platform = null,
        string? tag = null,
        string? query = null)
    {
        var normalizedQuery = string.IsNullOrWhiteSpace(query) ? null : LiveStream.NormalizeSearch(query);
        IEnumerable<int> candidates = Enumerable.Range(0, Streams.Length);

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var key = LiveStream.NormalizeSearch(tag);
            if (!StreamsByTag.TryGetValue(key, out var indexed))
            {
                return [];
            }

            candidates = indexed;
        }

        return candidates
            .Select(index => Streams[index])
            .Where(stream => platform is null || stream.Platform == platform)
            .Where(stream => normalizedQuery is null || stream.SearchText.Contains(normalizedQuery, StringComparison.Ordinal))
            .ToImmutableArray();
    }
}
