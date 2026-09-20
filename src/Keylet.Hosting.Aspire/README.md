# Keylet.Hosting.Aspire

Run the [`arturek/keylet`](https://hub.docker.com/r/arturek/keylet) development OpenID Connect provider from a .NET Aspire AppHost.

```csharp
using Keylet.Hosting.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

var keylet = builder.AddKeylet("keylet", options =>
{
    options.ImageTag = "1.0.0";
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
