# Keylet

Keylet is a deliberately insecure, configuration-only OpenID Connect provider for development and automated tests. It behaves like the useful OIDC surface of Keyla without Identity, PostgreSQL, passwords, account administration, or dynamic client registration.

It supports:

- authorization code flow with S256 PKCE;
- discovery, JWKS, token, userinfo, and end-session endpoints;
- confidential and public configured clients;
- configured `sub`, name, username, email, verification, and role claims;
- an interactive one-click account picker;
- automatic login with a default user and optional `login_hint` selection;
- an in-memory event view at `/events`;
- structured event logs, custom event spans, ASP.NET Core spans, runtime/HTTP metrics, and OTLP export;
- health endpoints at `/health` and `/alive`;
- an Aspire AppHost and an OCI image definition.

> Keylet provides no authentication before impersonation. Never expose it as a production or shared authority.

## Run with Aspire

```powershell
aspire start --non-interactive
aspire wait keylet --non-interactive
```

Use the HTTPS endpoint shown by `aspire describe` as the OIDC authority. A consuming project in the same AppHost can receive it without hard-coding the Aspire-assigned port:

```csharp
var keylet = builder.AddProject<Projects.Keylet>("keylet")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.MyApp>("app")
    .WithEnvironment("Authentication__Keyla__Authority", keylet.GetEndpoint("https"))
    .WithEnvironment("Authentication__Keyla__ClientId", "sample-web")
    .WithEnvironment("Authentication__Keyla__ClientSecret", "sample-web-secret")
    .WaitFor(keylet);
```

The application's browser callback URI still has to appear in `Keylet:Clients[*]:RedirectUris`.

## Configuration

Configuration is read at startup from normal ASP.NET Core providers. JSON is clearest for lists; environment-variable overrides use double underscores, for example `Keylet__Mode=Automatic`.

```json
{
  "Keylet": {
    "Issuer": "https://keylet.example.test",
    "Mode": "Interactive",
    "AutomaticUser": "admin",
    "AllowLoginHint": true,
    "AllowInsecureHttp": false,
    "EventCapacity": 500,
    "Users": [
      {
        "Subject": "admin",
        "Name": "Test Administrator",
        "PreferredUsername": "admin",
        "Email": "admin@example.test",
        "EmailVerified": true,
        "Roles": [ "Admin", "User" ]
      }
    ],
    "Clients": [
      {
        "ClientId": "my-app",
        "ClientSecret": "local-test-secret",
        "DisplayName": "My application",
        "RedirectUris": [ "https://localhost:7180/signin-oidc" ],
        "PostLogoutRedirectUris": [ "https://localhost:7180/signout-callback-oidc" ],
        "AllowedScopes": [ "openid", "profile", "email", "roles" ],
        "AllowRefreshTokens": false,
        "RequirePkce": true
      }
    ]
  }
}
```

`Issuer` is optional. When omitted, OpenIddict derives it from the request, which works well with Aspire's endpoint proxy. Set it when the service runs behind a stable reverse-proxy address. Plain HTTP is accepted only when `AllowInsecureHttp` is true; a consuming ASP.NET Core OIDC handler then also needs `RequireHttpsMetadata = false`.

Client secrets are intentionally fixed test data. The configuration page shows whether a client is public or confidential but never renders its secret.

Signing and encryption keys, grants, and event history are ephemeral. Restarting Keylet invalidates previously issued tokens, which keeps test runs isolated.

### Automated mode

Set `Keylet:Mode` to `Automatic`. Authorization requests immediately use `AutomaticUser`. When `AllowLoginHint` is true, a request may select another configured identity by subject, preferred username, or email:

```text
/connect/authorize?...&login_hint=user
```

This keeps browser automation out of most integration tests while allowing multi-user scenarios. It does not weaken PKCE or redirect URI validation.

## Events and OpenTelemetry

`/events` shows the bounded in-memory identity/protocol timeline. It includes configuration load, interactive or automatic account selection, authorization, token, userinfo, discovery/JWKS, and logout traffic. It never stores authorization codes, tokens, PKCE verifiers, or client secrets.

Every event is also emitted as a structured `Information` log and a span from the `Keylet.Events` activity source. ServiceDefaults also emits ASP.NET Core/HTTP/runtime telemetry. Set the standard exporter variables to send all of it to an OTLP collector:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=http://collector:4317
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
OTEL_SERVICE_NAME=keylet
```

## Image

Build with the repository root as context:

```powershell
wslc build -f src/Keylet/Dockerfile -t keylet:local .
```

The image listens on port `8080`. Mount a JSON configuration file or provide indexed environment variables such as `Keylet__Users__0__Subject` and `Keylet__Clients__0__RedirectUris__0`.

## Validation

```powershell
dotnet build Keylet.slnx --no-restore
dotnet test --project tests\Keylet.Tests\Keylet.Tests.csproj --no-build
```
