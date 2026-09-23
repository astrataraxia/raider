using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Raider.Web.Chzzk;
using Raider.Web.Collection;
using Raider.Web.Configuration;
using Raider.Web.Favorites;
using Raider.Web.Live;
using Raider.Web.Recap;
using Raider.Web.Soop;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddOptions<ChzzkOptions>()
    .Bind(builder.Configuration.GetSection(ChzzkOptions.SectionName));
builder.Services
    .AddOptions<SoopOptions>()
    .Bind(builder.Configuration.GetSection(SoopOptions.SectionName));
builder.Services
    .AddOptions<ChatOptions>()
    .Bind(builder.Configuration.GetSection(ChatOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CollectionRegistry>();
builder.Services.AddRazorPages();
builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
var dataProtectionDirectory = ResolveDataProtectionDirectory(builder.Configuration);
if (dataProtectionDirectory is not null)
{
    builder.Services.AddDataProtection()
        .SetApplicationName("Raider")
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionDirectory));
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "raider-recap";
        options.LoginPath = "/recap";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.AddSingleton(services => new FavoriteStore(ResolveDatabasePath(services)));
builder.Services.AddSingleton(services => new ChatCountStore(ResolveDatabasePath(services)));
builder.Services.AddSingleton<FavoriteCatalog>();
builder.Services.AddSingleton<OauthStateStore>();
builder.Services.AddHttpClient<ChzzkClient>(client =>
{
    client.BaseAddress = new Uri("https://openapi.chzzk.naver.com/");
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddTransient<ILiveSource>(services => services.GetRequiredService<ChzzkClient>());
builder.Services
    .AddHttpClient<SoopClient>(client =>
    {
        client.BaseAddress = new Uri("https://openapi.sooplive.com/");
        client.Timeout = Timeout.InfiniteTimeSpan;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseCookies = false,
    });
builder.Services.AddTransient<ILiveSource>(services => services.GetRequiredService<SoopClient>());
builder.Services.AddSingleton(_ => new SnapshotStore([Platform.Chzzk, Platform.Soop]));
builder.Services.AddSingleton<IHostedService>(services => CreateCollector(
    services,
    services.GetRequiredService<ChzzkClient>(),
    "Raider:Collection:Chzzk",
    new CollectionOptions()));
builder.Services.AddSingleton<IHostedService>(services => CreateCollector(
    services,
    services.GetRequiredService<SoopClient>(),
    "Raider:Collection:Soop",
    new CollectionOptions { CollectionTimeout = TimeSpan.FromSeconds(30) }));
builder.Services.AddHttpClient<ChzzkChatAccess>(client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
    if (client.DefaultRequestHeaders.UserAgent.Count == 0)
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Raider/0.1");
    }
});
builder.Services.AddHttpClient<ChzzkAuthClient>(client =>
{
    client.BaseAddress = new Uri("https://openapi.chzzk.naver.com/");
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddHostedService<ChzzkChatWorker>();

var app = builder.Build();

app.Logger.LogInformation(
    "Data protection keys directory: {Directory}",
    dataProtectionDirectory ?? "ephemeral");
try
{
    await app.Services.GetRequiredService<FavoriteStore>().InitializeAsync(CancellationToken.None);
    await app.Services.GetRequiredService<ChatCountStore>().InitializeAsync(CancellationToken.None);
}
catch (Exception exception)
{
    app.Logger.LogError(exception, "Store initialization failed.");
}

app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapRazorPages();
app.MapFavoriteEndpoints();
app.MapChzzkAuthEndpoints();
app.MapGet("/favicon.ico", () => Results.Redirect("/favicon.svg"));
app.MapGet("/health/live", () => Results.Ok());
app.MapGet("/health/ready", (SnapshotStore snapshots) =>
    snapshots.Current.IsReady ? Results.Ok() : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.MapGet("/api/refresh/status", (CollectionRegistry registry, SnapshotStore snapshots) => Results.Json(new
{
    isRefreshing = registry.IsAnyCollecting,
    snapshotVersion = snapshots.Current.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
    platforms = snapshots.Current.Platforms.Values
        .OrderBy(state => state.Platform)
        .Select(state => new
        {
            platform = state.Platform.ToString(),
            result = state.LastAttemptAt is null ? "Pending" : state.Error is null ? "Success" : "Failure",
            durationMs = state.LastDuration?.TotalMilliseconds,
            errorKind = state.Error?.Kind.ToString(),
        }),
}));

app.Run();

static string? ResolveDataProtectionDirectory(IConfiguration configuration)
{
    var candidates = new List<string>();
    var databasePath = configuration["Raider:Favorites:DatabasePath"];
    if (!string.IsNullOrWhiteSpace(databasePath))
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrWhiteSpace(parent))
        {
            candidates.Add(Path.Combine(parent, "dp-keys"));
        }
    }

    candidates.Add("/data/dp-keys");
    candidates.Add(Path.Combine(Path.GetTempPath(), "raider-dp-keys"));
    foreach (var candidate in candidates.Distinct(StringComparer.Ordinal))
    {
        var created = CreateWritableDirectory(candidate);
        if (created is not null)
        {
            return created;
        }
    }

    return null;
}

static string? CreateWritableDirectory(string path)
{
    try
    {
        Directory.CreateDirectory(path);
        var probe = Path.Combine(path, ".write-test");
        File.WriteAllText(probe, "ok");
        File.Delete(probe);
        return path;
    }
    catch (Exception)
    {
        return null;
    }
}

static string ResolveDatabasePath(IServiceProvider services)
    => services.GetRequiredService<IConfiguration>()["Raider:Favorites:DatabasePath"]
        ?? Path.Combine(AppContext.BaseDirectory, "data", "raider.db");

static PlatformCollectorWorker CreateCollector(
    IServiceProvider services,
    ILiveSource source,
    string section,
    CollectionOptions fallback)
{
    return new PlatformCollectorWorker(
        source,
        services.GetRequiredService<SnapshotStore>(),
        services.GetRequiredService<IConfiguration>().GetSection(section).Get<CollectionOptions>() ?? fallback,
        services.GetRequiredService<CollectionRegistry>(),
        services.GetRequiredService<TimeProvider>(),
        services.GetRequiredService<ILogger<PlatformCollectorWorker>>());
}

public partial class Program;
