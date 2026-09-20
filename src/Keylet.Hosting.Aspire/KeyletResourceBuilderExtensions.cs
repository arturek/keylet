using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting;

namespace Keylet.Hosting.Aspire;

public static class KeyletResourceBuilderExtensions
{
    public const string DefaultImage = "arturek/keylet";
    public const string DefaultImageTag = "latest";
    public const string DefaultEndpointName = "http";
    public const int DefaultTargetPort = 8080;

    public static IResourceBuilder<ContainerResource> AddKeylet(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        Action<KeyletResourceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var options = new KeyletResourceOptions();
        configure?.Invoke(options);

        var keylet = builder.AddContainer(name, options.Image, options.ImageTag)
            .WithHttpEndpoint(
                targetPort: options.TargetPort,
                name: options.EndpointName)
            .WithHttpHealthCheck(options.HealthPath, endpointName: options.EndpointName);

        if (options.IsExternal)
        {
            keylet = keylet.WithExternalHttpEndpoints();
        }

        ApplyConfiguration(keylet, options.Configuration);
        return keylet;
    }

    public static IResourceBuilder<T> WithKeyletAuthentication<T, TKeylet>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<TKeylet> keylet,
        string clientId,
        string? clientSecret = null,
        string configurationSection = "Authentication__Keylet",
        string endpointName = DefaultEndpointName,
        bool requireHttpsMetadata = false)
        where T : IResourceWithEnvironment
        where TKeylet : IResourceWithEndpoints
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(keylet);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationSection);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

        builder.WithEnvironment(
            $"{configurationSection}__Authority",
            keylet.GetEndpoint(endpointName));
        builder.WithEnvironment($"{configurationSection}__ClientId", clientId);
        builder.WithEnvironment(
            $"{configurationSection}__RequireHttpsMetadata",
            requireHttpsMetadata.ToString());

        if (clientSecret is not null)
        {
            builder.WithEnvironment($"{configurationSection}__ClientSecret", clientSecret);
        }

        return builder;
    }

    public static IResourceBuilder<T> WithKeyletClient<T>(
        this IResourceBuilder<T> keylet,
        EndpointReference applicationEndpoint,
        string clientId,
        string? clientSecret = null,
        int clientIndex = 0,
        string redirectPath = "/signin-oidc",
        string postLogoutRedirectPath = "/signout-callback-oidc",
        EndpointReference? additionalApplicationEndpoint = null)
        where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(keylet);
        ArgumentNullException.ThrowIfNull(applicationEndpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentOutOfRangeException.ThrowIfNegative(clientIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(postLogoutRedirectPath);

        var prefix = $"Keylet__Clients__{clientIndex}__";
        keylet.WithEnvironment($"{prefix}ClientId", clientId);
        keylet.WithEnvironment(
            $"{prefix}RedirectUris__0",
            $"{applicationEndpoint}{redirectPath}");
        keylet.WithEnvironment(
            $"{prefix}PostLogoutRedirectUris__0",
            $"{applicationEndpoint}{postLogoutRedirectPath}");

        if (additionalApplicationEndpoint is not null)
        {
            keylet.WithEnvironment(
                $"{prefix}RedirectUris__1",
                $"{additionalApplicationEndpoint}{redirectPath}");
            keylet.WithEnvironment(
                $"{prefix}PostLogoutRedirectUris__1",
                $"{additionalApplicationEndpoint}{postLogoutRedirectPath}");
        }

        if (clientSecret is not null)
        {
            keylet.WithEnvironment($"{prefix}ClientSecret", clientSecret);
        }

        return keylet;
    }

    public static IResourceBuilder<T> WithKeyletClient<T, TApplication>(
        this IResourceBuilder<T> keylet,
        IResourceBuilder<TApplication> application,
        string clientId,
        string? clientSecret = null,
        int clientIndex = 0,
        string applicationEndpointName = "https",
        string redirectPath = "/signin-oidc",
        string postLogoutRedirectPath = "/signout-callback-oidc")
        where T : IResourceWithEnvironment
        where TApplication : IResourceWithEndpoints
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationEndpointName);

        return keylet.WithKeyletClient(
            application.GetEndpoint(applicationEndpointName, KnownNetworkIdentifiers.LocalhostNetwork),
            clientId,
            clientSecret,
            clientIndex,
            redirectPath,
            postLogoutRedirectPath,
            application.GetEndpoint(applicationEndpointName, KnownNetworkIdentifiers.DefaultAspireContainerNetwork));
    }

    private static void ApplyConfiguration<T>(
        IResourceBuilder<T> keylet,
        KeyletConfiguration configuration)
        where T : IResourceWithEnvironment
    {
        SetIfNotNull(keylet, "Keylet__Issuer", configuration.Issuer);
        SetIfNotNull(keylet, "Keylet__Mode", configuration.Mode?.ToString());
        SetIfNotNull(keylet, "Keylet__AutomaticUser", configuration.AutomaticUser);
        SetIfNotNull(keylet, "Keylet__AllowLoginHint", configuration.AllowLoginHint?.ToString());
        SetIfNotNull(keylet, "Keylet__AllowInsecureHttp", configuration.AllowInsecureHttp?.ToString());
        SetIfNotNull(keylet, "Keylet__EventCapacity", configuration.EventCapacity?.ToString());

        foreach (var (name, value) in configuration.AdditionalEnvironment)
        {
            keylet.WithEnvironment(name, value);
        }

        for (var i = 0; i < configuration.Users.Count; i++)
        {
            var user = configuration.Users[i];
            var prefix = $"Keylet__Users__{i}__";
            keylet.WithEnvironment($"{prefix}Subject", user.Subject);
            keylet.WithEnvironment($"{prefix}Name", user.Name);
            SetIfNotNull(keylet, $"{prefix}PreferredUsername", user.PreferredUsername);
            SetIfNotNull(keylet, $"{prefix}Email", user.Email);
            SetIfNotNull(keylet, $"{prefix}EmailVerified", user.EmailVerified?.ToString());
            SetCollection(keylet, $"{prefix}Roles", user.Roles);
        }

        for (var i = 0; i < configuration.Clients.Count; i++)
        {
            var client = configuration.Clients[i];
            var prefix = $"Keylet__Clients__{i}__";
            keylet.WithEnvironment($"{prefix}ClientId", client.ClientId);
            SetIfNotNull(keylet, $"{prefix}ClientSecret", client.ClientSecret);
            SetIfNotNull(keylet, $"{prefix}DisplayName", client.DisplayName);
            SetIfNotNull(keylet, $"{prefix}AllowRefreshTokens", client.AllowRefreshTokens?.ToString());
            SetIfNotNull(keylet, $"{prefix}RequirePkce", client.RequirePkce?.ToString());
            SetCollection(keylet, $"{prefix}RedirectUris", client.RedirectUris);
            SetCollection(keylet, $"{prefix}PostLogoutRedirectUris", client.PostLogoutRedirectUris);
            SetCollection(keylet, $"{prefix}AllowedScopes", client.AllowedScopes);
        }
    }

    private static void SetCollection<T>(
        IResourceBuilder<T> keylet,
        string prefix,
        IEnumerable<string> values)
        where T : IResourceWithEnvironment
    {
        var index = 0;
        foreach (var value in values)
        {
            keylet.WithEnvironment($"{prefix}__{index++}", value);
        }
    }

    private static void SetIfNotNull<T>(
        IResourceBuilder<T> keylet,
        string name,
        string? value)
        where T : IResourceWithEnvironment
    {
        if (value is not null)
        {
            keylet.WithEnvironment(name, value);
        }
    }
}
