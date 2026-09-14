namespace Raider.Web.Configuration;

public sealed class ChzzkOptions
{
    public const string SectionName = "Raider:Chzzk";

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
