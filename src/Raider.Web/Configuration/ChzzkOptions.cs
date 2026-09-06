// CHZZK Client 인증 설정을 검증하고 비밀값 출력을 방지한다.
namespace Raider.Web.Configuration;

public sealed class ChzzkOptions
{
    public const string SectionName = "Raider:Chzzk";

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException("CHZZK ClientId is required.");
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new InvalidOperationException("CHZZK ClientSecret is required.");
        }
    }

    public override string ToString()
    {
        return nameof(ChzzkOptions);
    }
}
