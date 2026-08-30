// SOOP 공식 API 인증 설정을 검증하고 비밀값 출력을 방지한다.
namespace Raider.Web.Configuration;

public sealed class SoopOptions
{
    public const string SectionName = "Raider:Soop";

    public string ClientId { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException("SOOP ClientId is required.");
        }
    }

    public override string ToString()
    {
        return nameof(SoopOptions);
    }
}
