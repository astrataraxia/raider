// 수집, 채팅, 저장, 최근 경고를 한 화면의 읽기 모델로 만든다.
using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Raider.Web.Collection;
using Raider.Web.Live;
using Raider.Web.Recap;
using Raider.Web.Status;

namespace Raider.Web.Pages;

public sealed class StatusModel(
    SnapshotStore snapshots,
    CollectionRegistry registry,
    TimeProvider timeProvider,
    ProcessStatus process,
    AppPaths paths,
    StoreGate store,
    ChzzkChatWorker chat,
    StatusLog log) : PageModel
{
    private static readonly TimeZoneInfo SeoulTimeZone = FindSeoulTimeZone();

    public string UptimeText { get; private set; } = "";

    public string MemoryText { get; private set; } = "";

    public string? MemoryLimitText { get; private set; }

    public bool IsRefreshing { get; private set; }

    public bool SyncIsWarning { get; private set; }

    public string SyncTitle { get; private set; } = "";

    public string SyncDetail { get; private set; } = "";

    public string? Notice { get; private set; }

    public IReadOnlyList<PlatformCard> Platforms { get; private set; } = [];

    public SideCard Chat { get; private set; } = null!;

    public SideCard Storage { get; private set; } = null!;

    public IReadOnlyList<StatusEvent> Warnings { get; private set; } = [];

    public void OnGet()
    {
        var now = timeProvider.GetUtcNow();
        var snapshot = snapshots.Current;
        UptimeText = FormatUptime(now - process.StartedAt);
        MemoryText = FormatMegabytes(process.MemoryBytes);
        MemoryLimitText = process.MemoryLimitBytes is long limit ? FormatMegabytes(limit) : null;
        IsRefreshing = registry.IsAnyCollecting;
        var hasConfigurationFailure = snapshot.Platforms.Values.Any(state => state.Error?.Kind == PlatformErrorKind.Configuration);
        var hasPartialFailure = snapshot.Live.Streams.Length > 0
            && snapshot.Platforms.Values.Any(state => state.Error is not null && state.Error.Kind != PlatformErrorKind.Configuration);
        var hasInitialFailure = snapshot.Live.Streams.Length == 0
            && snapshot.Platforms.Values.Any(state => state.Error is not null);
        var isStale = snapshot.Platforms.Values
            .Where(state => state.LastSuccessAt is not null)
            .Any(state => now - state.LastSuccessAt > CollectionSnapshot.StaleAfter);
        SyncIsWarning = hasConfigurationFailure || hasPartialFailure || hasInitialFailure || isStale;
        SyncTitle = hasConfigurationFailure
            ? "일부 플랫폼 오류"
            : hasPartialFailure || hasInitialFailure ? "일부 수집 지연" : "현재 스냅샷";
        SyncDetail = snapshot.Platforms.Values.Any(state => state.AttemptCompleted)
            ? FormatClock(snapshot.ObservedAt, "HH:mm") + " 갱신"
            : "아직 없음";
        Notice = BuildNotice(snapshot, now);
        Platforms = snapshot.Platforms.Values
            .OrderBy(state => state.Platform)
            .Select(state => BuildPlatform(state, now))
            .ToArray();
        Chat = BuildChat(chat.ReadActivity());
        Storage = BuildStorage();
        Warnings = log.Recent();
    }

    public string FormatEventTime(DateTimeOffset at) => FormatClock(at, "HH:mm:ss");

    private PlatformCard BuildPlatform(PlatformCollectionState state, DateTimeOffset now)
    {
        var name = PlatformName(state.Platform);
        var stale = state.LastSuccessAt is not null && now - state.LastSuccessAt > CollectionSnapshot.StaleAfter;
        var (pill, pillClass) = PillFor(state, stale);
        var rows = new List<StatusRow>();
        if (state.LastSuccessAt is not null)
        {
            rows.Add(new StatusRow("마지막 성공", FormatClock(state.LastSuccessAt.Value, "M'월' d'일' HH:mm")));
        }

        var showingPrevious = state.Error is not null && !state.IsPartial && state.LastSuccessAt is not null;
        rows.Add(new StatusRow(
            showingPrevious ? "보여주는 목록" : "방송",
            showingPrevious
                ? $"{state.Streams.Length.ToString("N0", CultureInfo.InvariantCulture)} · 이전 성공분"
                : state.Streams.Length.ToString("N0", CultureInfo.InvariantCulture)));
        if (state.Error is not null && !state.IsPartial)
        {
            rows.Add(new StatusRow("오류", state.Error.Kind.ToString()));
        }

        if (state.LastSuccessAt is not null && (state.Error is not null || stale) && !state.IsPartial)
        {
            rows.Add(new StatusRow("성공 후 경과", $"{FormatAge(now - state.LastSuccessAt.Value)} / 20분"));
        }

        return new PlatformCard(name, DotClass(state.Platform), pillClass, pill, LeadFor(state, now), rows);
    }

    private static (string Pill, string PillClass) PillFor(PlatformCollectionState state, bool stale)
    {
        if (state.IsPartial)
        {
            return ("수집 중", "ok");
        }

        if (!state.AttemptCompleted)
        {
            return ("대기", "wait");
        }

        if (state.Error is null)
        {
            return stale ? ("오래됨", "warn") : ("성공", "ok");
        }

        return (KindText(state.Error.Kind), "warn");
    }

    private static string LeadFor(PlatformCollectionState state, DateTimeOffset now)
    {
        if (state.IsPartial)
        {
            return "페이지를 받는 중입니다.";
        }

        if (state.LastAttemptAt is null)
        {
            return "아직 수집하지 않았습니다.";
        }

        var when = Ago(now - state.LastAttemptAt.Value);
        var duration = state.LastDuration is TimeSpan elapsed ? FormatDuration(elapsed) : null;
        if (state.Error is null)
        {
            return duration is null ? $"{when} 수집" : $"{when} 수집 · {duration}";
        }

        if (duration is null)
        {
            return $"{when} 시도";
        }

        return state.Error.Kind == PlatformErrorKind.Timeout
            ? $"{when} 시도 · {duration}에서 중단"
            : $"{when} 시도 · {duration}";
    }

    private static string? BuildNotice(CollectionSnapshot snapshot, DateTimeOffset now)
    {
        if (snapshot.Platforms.Values.All(state => !state.AttemptCompleted))
        {
            return "첫 수집을 기다리는 중입니다.";
        }

        var parts = new List<string>();
        foreach (var state in snapshot.Platforms.Values.OrderBy(state => state.Platform))
        {
            if (state.IsPartial || state.Error is null)
            {
                if (state.Error is null
                    && state.LastSuccessAt is not null
                    && now - state.LastSuccessAt > CollectionSnapshot.StaleAfter)
                {
                    parts.Add($"{PlatformName(state.Platform)} 마지막 성공이 20분을 넘었습니다.");
                }

                continue;
            }

            var name = PlatformName(state.Platform);
            if (state.Error.Kind == PlatformErrorKind.Configuration)
            {
                parts.Add($"{name} API 키가 설정되지 않았습니다. 해당 플랫폼 방송을 수집할 수 없습니다.");
                continue;
            }

            var kind = KindText(state.Error.Kind);
            if (state.LastSuccessAt is null)
            {
                parts.Add($"{name} 수집이 {kind}로 끝났습니다. 보여줄 이전 목록이 없습니다.");
                continue;
            }

            var when = Ago(now - state.LastSuccessAt.Value);
            var received = when == "방금" ? "방금 받은" : when;
            parts.Add($"{name} 수집이 {kind}로 끝났습니다. 홈은 {received} 목록 {state.Streams.Length.ToString("N0", CultureInfo.InvariantCulture)}개를 그대로 보여 줍니다. 20분이 지나면 stale입니다.");
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private SideCard BuildChat(ChzzkChatWorker.ChatActivity activity)
    {
        string pill;
        string pillClass;
        string lead;
        if (!activity.Enabled)
        {
            pill = "꺼짐";
            pillClass = "wait";
            lead = "채팅 수집이 꺼져 있습니다.";
        }
        else if (activity.LastTickAt is null)
        {
            pill = "확인 전";
            pillClass = "wait";
            lead = "첫 확인을 기다리는 중입니다.";
        }
        else if (activity.LastTickFailed)
        {
            pill = "오류";
            pillClass = "warn";
            lead = "마지막 확인에 실패했습니다.";
        }
        else if (activity.Connected < activity.Wanted)
        {
            pill = "재연결";
            pillClass = "warn";
            lead = $"즐겨찾기 라이브 {activity.Wanted.ToString(CultureInfo.InvariantCulture)}곳 중 {activity.Connected.ToString(CultureInfo.InvariantCulture)}곳만 연결되어 있습니다.";
        }
        else if (activity.Wanted == 0)
        {
            pill = "대기";
            pillClass = "wait";
            lead = "지금 채팅을 받을 라이브가 없습니다.";
        }
        else
        {
            pill = "연결됨";
            pillClass = "ok";
            lead = $"즐겨찾기 라이브 {activity.Wanted.ToString(CultureInfo.InvariantCulture)}곳의 소켓이 열려 있습니다.";
        }

        var flush = activity.LastFlushAt is null
            ? "아직 없음"
            : activity.LastFlushSucceeded == false
                ? $"{FormatClock(activity.LastFlushAt.Value, "HH:mm")} 실패"
                : $"{FormatClock(activity.LastFlushAt.Value, "HH:mm")} 성공";
        return new SideCard(
            "CHZZK 채팅",
            pillClass,
            pill,
            lead,
            [
                new StatusRow("소켓", $"{activity.Connected.ToString(CultureInfo.InvariantCulture)} / {activity.Wanted.ToString(CultureInfo.InvariantCulture)}"),
                new StatusRow("마지막 flush", flush),
                new StatusRow("아직 안 쓴 채팅", $"{activity.PendingCount.ToString("N0", CultureInfo.InvariantCulture)}건"),
            ]);
    }

    private SideCard BuildStorage()
    {
        var database = new FileInfo(paths.DatabasePath);
        var databaseText = database.Exists ? FormatByteCount(database.Length) : "파일 없음";
        string pill;
        string pillClass;
        string lead;
        if (!store.IsReady)
        {
            pill = "실패";
            pillClass = "warn";
            lead = "SQLite 초기화에 실패했습니다. 즐겨찾기와 채팅 건수를 저장할 수 없습니다.";
        }
        else if (paths.KeyDirectory is null)
        {
            pill = "주의";
            pillClass = "warn";
            lead = "데이터베이스는 쓸 수 있습니다. 키 디렉터리는 없습니다.";
        }
        else
        {
            pill = "정상";
            pillClass = "ok";
            lead = "SQLite와 키 디렉터리를 쓸 수 있습니다.";
        }

        return new SideCard(
            "저장",
            pillClass,
            pill,
            lead,
            [
                new StatusRow(database.Name, databaseText),
                new StatusRow("남은 용량", FreeBytes(database) is long free ? FormatByteCount(free) : "확인 불가"),
                new StatusRow("키 디렉터리", paths.KeyDirectory ?? "없음"),
            ]);
    }

    private static long? FreeBytes(FileInfo database)
    {
        try
        {
            var directory = database.Directory?.FullName ?? Directory.GetCurrentDirectory();
            var root = Path.GetPathRoot(Path.GetFullPath(directory));
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var drive = new DriveInfo(root);
            return drive.IsReady ? drive.AvailableFreeSpace : null;
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

    private static string PlatformName(Platform platform) => platform switch
    {
        Platform.Chzzk => "CHZZK",
        Platform.Soop => "SOOP",
        _ => platform.ToString(),
    };

    private static string DotClass(Platform platform) => platform switch
    {
        Platform.Chzzk => "chzzk-dot",
        Platform.Soop => "soop-dot",
        _ => "",
    };

    private static string KindText(PlatformErrorKind kind) => kind switch
    {
        PlatformErrorKind.Network => "네트워크 오류",
        PlatformErrorKind.Server => "서버 오류",
        PlatformErrorKind.Timeout => "시간 초과",
        PlatformErrorKind.Authentication => "인증 오류",
        PlatformErrorKind.Forbidden => "권한 없음",
        PlatformErrorKind.RateLimited => "호출 제한",
        PlatformErrorKind.Contract => "응답 형식 오류",
        PlatformErrorKind.Domain => "데이터 오류",
        PlatformErrorKind.Configuration => "키 없음",
        _ => kind.ToString(),
    };

    private static string Ago(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
        {
            return "방금";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes}분 전";
        }

        if (age < TimeSpan.FromDays(1))
        {
            return $"{(int)age.TotalHours}시간 전";
        }

        return $"{(int)age.TotalDays}일 전";
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
        {
            return "1분 미만";
        }

        if (age < TimeSpan.FromDays(1))
        {
            return $"{(int)age.TotalMinutes}분";
        }

        return $"{(int)age.TotalDays}일";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromSeconds(10))
        {
            return $"{duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)}초";
        }

        if (duration < TimeSpan.FromMinutes(1))
        {
            return $"{((int)Math.Round(duration.TotalSeconds)).ToString(CultureInfo.InvariantCulture)}초";
        }

        return $"{((int)Math.Round(duration.TotalMinutes)).ToString(CultureInfo.InvariantCulture)}분";
    }

    private static string FormatUptime(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
        {
            return "1분 미만";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes}분";
        }

        if (age < TimeSpan.FromDays(1))
        {
            return age.Minutes == 0
                ? $"{(int)age.TotalHours}시간"
                : $"{(int)age.TotalHours}시간 {age.Minutes}분";
        }

        return age.Hours == 0
            ? $"{(int)age.TotalDays}일"
            : $"{(int)age.TotalDays}일 {age.Hours}시간";
    }

    private static string FormatMegabytes(long bytes)
    {
        var megabytes = bytes / (1024d * 1024d);
        if (megabytes < 1)
        {
            return "1 MB 미만";
        }

        return $"{((int)Math.Round(megabytes)).ToString(CultureInfo.InvariantCulture)} MB";
    }

    private static string FormatByteCount(long value)
    {
        if (value >= 1_000_000_000)
        {
            return $"{(value / 1_000_000_000d).ToString("0.0", CultureInfo.InvariantCulture)} GB";
        }

        if (value >= 1_000_000)
        {
            return $"{(value / 1_000_000d).ToString("0.0", CultureInfo.InvariantCulture)} MB";
        }

        if (value >= 1_000)
        {
            return $"{(value / 1_000d).ToString("0.0", CultureInfo.InvariantCulture)} KB";
        }

        return $"{value.ToString(CultureInfo.InvariantCulture)} B";
    }

    private static string FormatClock(DateTimeOffset value, string format)
        => TimeZoneInfo.ConvertTime(value, SeoulTimeZone).ToString(format, CultureInfo.InvariantCulture);

    private static TimeZoneInfo FindSeoulTimeZone()
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

    public sealed record StatusRow(string Label, string Value);

    public sealed record PlatformCard(string Name, string DotClass, string PillClass, string Pill, string Lead, IReadOnlyList<StatusRow> Rows);

    public sealed record SideCard(string Title, string PillClass, string Pill, string Lead, IReadOnlyList<StatusRow> Rows);
}
