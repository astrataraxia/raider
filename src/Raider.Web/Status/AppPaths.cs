// 상태 화면이 읽는 데이터베이스와 키 디렉터리 경로다.
namespace Raider.Web.Status;

public sealed record AppPaths(string DatabasePath, string? KeyDirectory);
