# Keylet.Hosting.Aspire

Run the [`arturek/keylet`](https://hub.docker.com/r/arturek/keylet) development OpenID Connect provider from a .NET Aspire AppHost.

## Features

- Adds the Keylet container to an Aspire AppHost without requiring a local image build.
- Supports image, tag, endpoint, port, health-check, and external-endpoint configuration.
- Provides typed provider configuration for users, clients, scopes, PKCE, automatic login, and login hints.
- Wires a consuming resource to Keylet with `WithKeyletAuthentication`.
- Registers browser redirect and post-logout URIs with `WithKeyletClient`.
- Handles both Aspire localhost and container-network callback addresses for project resources.

<p><img src="images/choose-identity.jpg" alt="Keylet identity picker" width="720"></p>

```csharp
using Keylet.Hosting.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

var keylet = builder.AddKeylet("keylet", options =>
{
    // To use a specific image tag, uncomment and change this value, for example:
    // options.ImageTag = "1.0.5-beta";
    options.Configuration.Mode = KeyletMode.Automatic;
    options.Configuration.AutomaticUser = "admin";
});

var app = builder.AddProject<Projects.MyApp>("app")
    .WithExternalHttpEndpoints()
    .WithKeyletAuthentication(keylet, "my-app", "local-development-secret")
    .WaitFor(keylet);

keylet.WithKeyletClient(
    app,
    clientId: "my-app",
    clientSecret: "local-development-secret");

builder.Build().Run();
```

The authentication helper defaults `RequireHttpsMetadata` to `false` because the image exposes HTTP for local development. Set `requireHttpsMetadata: true` when Keylet is fronted by HTTPS.

The integration uses the `arturek/keylet` image, exposes its HTTP endpoint, adds a `/health` check, and provides the endpoint reference needed by consuming Aspire resources. Keylet is deliberately insecure and is intended only for development and automated tests.
