// Docker Compose 기본 배포가 즐겨찾기 데이터 권한을 자동 준비하는지 검증한다.
namespace Raider.Web.Tests.Deployment;

public sealed class ComposeContractTests
{
    [Fact]
    public void ComposePreparesFavoriteDataBeforeStartingApp()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(repositoryRoot, "docker-compose.yml"));
        var buildOverride = File.ReadAllText(Path.Combine(repositoryRoot, "docker-compose.build.yml"));

        Assert.Contains("raider-data-init:", compose, StringComparison.Ordinal);
        Assert.Contains("chown app:app /data;", compose, StringComparison.Ordinal);
        Assert.Contains("chown app:app /data/raider.db;", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("chown -R", compose, StringComparison.Ordinal);
        Assert.Contains("exit 0", compose, StringComparison.Ordinal);
        Assert.Contains("condition: service_completed_successfully", compose, StringComparison.Ordinal);
        Assert.Contains("raider-data-init:", buildOverride, StringComparison.Ordinal);
        Assert.Contains("pull_policy: never", buildOverride, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Raider.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Raider.slnx를 포함한 저장소 루트를 찾지 못했습니다.");
    }
}
