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
builder.Services
    .AddOptions<SoopOptions>()
    .Bind(builder.Configuration.GetSection(SoopOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CollectionRegistry>();
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.AddSingleton(services => new FavoriteStore(
    builder.Configuration["Raider:Favorites:DatabasePath"] ?? Path.Combine(AppContext.BaseDirectory, "data", "raider.db")));
builder.Services.AddSingleton<FavoriteCatalog>();
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

var app = builder.Build();

try
{
    await app.Services.GetRequiredService<FavoriteStore>().InitializeAsync(CancellationToken.None);
}
catch (Exception exception)
{
    app.Logger.LogError(exception, "Favorite store initialization failed.");
}

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
