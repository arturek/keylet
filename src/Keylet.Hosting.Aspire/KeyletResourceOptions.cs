namespace Keylet.Hosting.Aspire;

public sealed class KeyletResourceOptions
{
    public string Image { get; set; } = "arturek/keylet";

    public string ImageTag { get; set; } = "latest";

    public int TargetPort { get; set; } = 8080;

    public string EndpointName { get; set; } = "http";

    public bool IsExternal { get; set; } = true;

    public string HealthPath { get; set; } = "/health";

    public KeyletConfiguration Configuration { get; } = new();
}

public sealed class KeyletConfiguration
{
    public string? Issuer { get; set; }

    public KeyletMode? Mode { get; set; }

    public string? AutomaticUser { get; set; }

    public bool? AllowLoginHint { get; set; }

    public bool? AllowInsecureHttp { get; set; }

    public int? EventCapacity { get; set; }

    public IList<KeyletUserConfiguration> Users { get; } = [];

    public IList<KeyletClientConfiguration> Clients { get; } = [];

    public IDictionary<string, string> AdditionalEnvironment { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public KeyletUserConfiguration AddUser(string subject, string name)
    {
        var user = new KeyletUserConfiguration(subject, name);
        Users.Add(user);
        return user;
    }

    public KeyletClientConfiguration AddClient(string clientId)
    {
        var client = new KeyletClientConfiguration(clientId);
        Clients.Add(client);
        return client;
    }
}

public enum KeyletMode
{
    Interactive,
    Automatic
}

public sealed class KeyletUserConfiguration(string subject, string name)
{
    public string Subject { get; } = subject;

    public string Name { get; } = name;

    public string? PreferredUsername { get; set; }

    public string? Email { get; set; }

    public bool? EmailVerified { get; set; }

    public IList<string> Roles { get; } = [];
}

public sealed class KeyletClientConfiguration(string clientId)
{
    public string ClientId { get; } = clientId;

    public string? ClientSecret { get; set; }

    public string? DisplayName { get; set; }

    public IList<string> RedirectUris { get; } = [];

    public IList<string> PostLogoutRedirectUris { get; } = [];

    public IList<string> AllowedScopes { get; } = ["openid", "profile", "email", "roles"];

    public bool? AllowRefreshTokens { get; set; }

    public bool? RequirePkce { get; set; }
}
