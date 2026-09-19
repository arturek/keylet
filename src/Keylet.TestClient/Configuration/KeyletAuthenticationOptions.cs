namespace Keylet.TestClient.Configuration;

public sealed class KeyletAuthenticationOptions
{
    public const string SectionName = "Authentication:Keylet";

    public string Authority { get; set; } = "";

    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    public bool RequireHttpsMetadata { get; set; } = true;
}

