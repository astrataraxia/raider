// Raider 웹 애플리케이션을 구성하고 실행한다.
using Raider.Web.Chzzk;
using Raider.Web.Collection;
using Raider.Web.Configuration;
using Raider.Web.Favorites;
using Raider.Web.Live;
using Raider.Web.Soop;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddOptions<ChzzkOptions>()
    .Bind(builder.Configuration.GetSection(ChzzkOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CollectionRegistry>();
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.AddSingleton(services => new FavoriteStore(
    builder.Configuration["Raider:Favorites:DatabasePath"] ?? Path.Combine(AppContext.BaseDirectory, "data", "raider.db")));
builder.Services.AddSingleton<FavoriteCatalog>();
builder.Services.AddSingleton<IHostedService, FavoriteStoreInitializer>();
builder.Services.AddHttpClient<ChzzkClient>(client =>
{
    client.BaseAddress = new Uri("https://openapi.chzzk.naver.com/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddTransient<ILiveSource>(services => services.GetRequiredService<ChzzkClient>());
builder.Services
    .AddHttpClient<SoopClient>(client =>
    {
        client.BaseAddress = new Uri("https://live.sooplive.com/");
        client.Timeout = TimeSpan.FromSeconds(15);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseCookies = false,
    });
builder.Services.AddTransient<ILiveSource>(services => services.GetRequiredService<SoopClient>());
builder.Services.AddSingleton(_ => new SnapshotStore([Platform.Chzzk, Platform.Soop]));
builder.Services.AddSingleton<IHostedService>(services => new PlatformCollectorWorker(
    services.GetRequiredService<ChzzkClient>(),
    services.GetRequiredService<SnapshotStore>(),
    services.GetRequiredService<IConfiguration>().GetSection("Raider:Collection:Chzzk").Get<CollectionOptions>() ?? new(),
    services.GetRequiredService<CollectionRegistry>(),
    services.GetRequiredService<TimeProvider>(),
    services.GetRequiredService<ILogger<PlatformCollectorWorker>>()));
builder.Services.AddSingleton<IHostedService>(services => new PlatformCollectorWorker(
    services.GetRequiredService<SoopClient>(),
    services.GetRequiredService<SnapshotStore>(),
    services.GetRequiredService<IConfiguration>().GetSection("Raider:Collection:Soop").Get<CollectionOptions>() ?? new()
    {
        CollectionTimeout = TimeSpan.FromSeconds(30),
    },
    services.GetRequiredService<CollectionRegistry>(),
    services.GetRequiredService<TimeProvider>(),
    services.GetRequiredService<ILogger<PlatformCollectorWorker>>()));

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorPages();
app.MapFavoriteEndpoints();
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

public partial class Program;
