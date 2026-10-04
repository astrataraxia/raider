// 프로세스 기동 시각, 작업 메모리, cgroup 메모리 한도를 읽는다.
namespace Raider.Web.Status;

public sealed class ProcessStatus(TimeProvider time)
{
    public DateTimeOffset StartedAt { get; } = time.GetUtcNow();

    public long MemoryBytes => Environment.WorkingSet;

    public long? MemoryLimitBytes => ReadMemoryLimit();

    internal static long? ReadMemoryLimit()
    {
        return ReadLimitFile("/sys/fs/cgroup/memory.max")
            ?? ReadLimitFile("/sys/fs/cgroup/memory/memory.limit_in_bytes");
    }

    private static long? ReadLimitFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var text = File.ReadAllText(path).Trim();
            if (text.Equals("max", StringComparison.Ordinal) || !long.TryParse(text, out var bytes))
            {
                return null;
            }

            if (bytes <= 0 || bytes >= 1L << 60)
            {
                return null;
            }

            return bytes;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
