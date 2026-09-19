namespace Keylet.Configuration;

public sealed class KeyletOptions
{
    public const string SectionName = "Keylet";

    public string? Issuer { get; set; }

    public KeyletMode Mode { get; set; } = KeyletMode.Interactive;

    public string? AutomaticUser { get; set; }

    public bool AllowLoginHint { get; set; } = true;

    public bool AllowInsecureHttp { get; set; } = true;

    public int EventCapacity { get; set; } = 500;

    public List<KeyletUserOptions> Users { get; set; } = [];

    public List<KeyletClientOptions> Clients { get; set; } = [];
}

public enum KeyletMode
{
    Interactive,
    Automatic
}

public sealed class KeyletUserOptions
{
    public string Subject { get; set; } = "";

    public string Name { get; set; } = "";

    public string? PreferredUsername { get; set; }

    public string? Email { get; set; }

    public bool EmailVerified { get; set; } = true;

    public List<string> Roles { get; set; } = [];
}

public sealed class KeyletClientOptions
{
    public string ClientId { get; set; } = "";

    public string? ClientSecret { get; set; }

    public string? DisplayName { get; set; }

    public List<string> RedirectUris { get; set; } = [];

    public List<string> PostLogoutRedirectUris { get; set; } = [];

    public List<string> AllowedScopes { get; set; } = ["openid", "profile", "email", "roles"];

    public bool AllowRefreshTokens { get; set; }

    public bool RequirePkce { get; set; } = true;
}

