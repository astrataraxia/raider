namespace Raider.Web.Configuration;

public sealed class SoopOptions
{
    public const string SectionName = "Raider:Soop";

    public string ClientId { get; init; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}
