// 즐겨찾기 저장소 초기화 실패를 애플리케이션의 나머지 기능과 격리한다.
namespace Raider.Web.Favorites;

public sealed class FavoriteStoreInitializer(FavoriteStore store, ILogger<FavoriteStoreInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await store.InitializeAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Favorite store initialization failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
