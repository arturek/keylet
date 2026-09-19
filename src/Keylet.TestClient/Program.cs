using Keylet.TestClient.Configuration;
using Keylet.TestClient.Events;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Keylet.TestClient;

public sealed class Program
{
    public const string CookieScheme = "KeyletTestClient.Cookie";
    public const string OpenIdConnectScheme = "Keylet";

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();
        builder.Services.AddRazorPages();
        builder.Services.AddSingleton<IClientEventStore, ClientEventStore>();
        builder.Services.AddSingleton<IValidateOptions<KeyletAuthenticationOptions>, KeyletAuthenticationOptionsValidator>();
        builder.Services.AddOptions<KeyletAuthenticationOptions>()
            .BindConfiguration(KeyletAuthenticationOptions.SectionName)
            .ValidateOnStart();

        var authentication = builder.Configuration
            .GetRequiredSection(KeyletAuthenticationOptions.SectionName)
            .Get<KeyletAuthenticationOptions>()
            ?? throw new InvalidOperationException("Keylet authentication configuration was not provided.");

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieScheme;
                options.DefaultAuthenticateScheme = CookieScheme;
                options.DefaultSignInScheme = CookieScheme;
                options.DefaultChallengeScheme = OpenIdConnectScheme;
            })
            .AddCookie(CookieScheme, options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.Name = "Keylet.TestClient.Session";
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;
            })
            .AddOpenIdConnect(OpenIdConnectScheme, options =>
            {
                options.Authority = authentication.Authority;
                options.ClientId = authentication.ClientId;
                options.ClientSecret = authentication.ClientSecret;
                options.RequireHttpsMetadata = authentication.RequireHttpsMetadata;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.ResponseMode = OpenIdConnectResponseMode.Query;
                options.CallbackPath = "/signin-oidc";
                options.SignedOutCallbackPath = "/signout-callback-oidc";
                options.UsePkce = true;
                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = false;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("roles");
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
                options.Events = CreateOpenIdConnectEvents();
            });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        _ = app.Services.GetRequiredService<IOptions<KeyletAuthenticationOptions>>().Value;
        app.Services.GetRequiredService<IClientEventStore>().Record(
            "client.started",
            "The OIDC test client started",
            new Dictionary<string, string?>
            {
                ["authority"] = authentication.Authority,
                ["client_id"] = authentication.ClientId
            });

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapRazorPages();
        app.MapDefaultEndpoints();

        await app.RunAsync();
    }

    private static OpenIdConnectEvents CreateOpenIdConnectEvents() => new()
    {
        OnRedirectToIdentityProvider = context =>
        {
            Record(
                context.HttpContext,
                "oidc.login-redirect",
                "Redirecting the browser to Keylet",
                new Dictionary<string, string?>
                {
                    ["client_id"] = context.ProtocolMessage.ClientId,
                    ["redirect_uri"] = context.ProtocolMessage.RedirectUri,
                    ["scope"] = context.ProtocolMessage.Scope
                });
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            Record(
                context.HttpContext,
                "oidc.message-received",
                "Received an OIDC callback message",
                new Dictionary<string, string?>
                {
                    ["path"] = context.Request.Path,
                    ["has_error"] = (!string.IsNullOrWhiteSpace(context.ProtocolMessage.Error)).ToString()
                });
            return Task.CompletedTask;
        },
        OnAuthorizationCodeReceived = context =>
        {
            Record(
                context.HttpContext,
                "oidc.authorization-code-received",
                "Received an authorization code; its value was not recorded",
                new Dictionary<string, string?>
                {
                    ["client_id"] = context.TokenEndpointRequest?.ClientId,
                    ["redirect_uri"] = context.TokenEndpointRequest?.RedirectUri,
                    ["pkce_verifier_present"] =
                        (context.TokenEndpointRequest?.Parameters.ContainsKey("code_verifier") is true).ToString()
                });
            return Task.CompletedTask;
        },
        OnTokenResponseReceived = context =>
        {
            Record(
                context.HttpContext,
                "oidc.token-response-received",
                "Keylet returned a token response; token values were not recorded",
                new Dictionary<string, string?>
                {
                    ["expires_in"] = context.TokenEndpointResponse.ExpiresIn?.ToString(),
                    ["scope"] = context.TokenEndpointResponse.Scope,
                    ["token_type"] = context.TokenEndpointResponse.TokenType
                });
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Record(
                context.HttpContext,
                "oidc.token-validated",
                "Validated the Keylet identity token",
                new Dictionary<string, string?>
                {
                    ["issuer"] = context.SecurityToken.Issuer,
                    ["subject"] = context.Principal?.FindFirst("sub")?.Value,
                    ["name"] = context.Principal?.Identity?.Name
                });
            return Task.CompletedTask;
        },
        OnUserInformationReceived = context =>
        {
            Record(
                context.HttpContext,
                "oidc.userinfo-received",
                "Received claims from the Keylet userinfo endpoint");
            return Task.CompletedTask;
        },
        OnTicketReceived = context =>
        {
            Record(
                context.HttpContext,
                "oidc.session-created",
                "Created the local client session",
                new Dictionary<string, string?>
                {
                    ["subject"] = context.Principal?.FindFirst("sub")?.Value,
                    ["return_uri"] = context.ReturnUri
                });
            return Task.CompletedTask;
        },
        OnRedirectToIdentityProviderForSignOut = context =>
        {
            Record(
                context.HttpContext,
                "oidc.logout-redirect",
                "Redirecting the browser to Keylet for logout",
                new Dictionary<string, string?>
                {
                    ["post_logout_redirect_uri"] = context.ProtocolMessage.PostLogoutRedirectUri
                });
            return Task.CompletedTask;
        },
        OnSignedOutCallbackRedirect = context =>
        {
            Record(
                context.HttpContext,
                "oidc.logout-callback",
                "Completed the Keylet logout callback");
            return Task.CompletedTask;
        },
        OnRemoteSignOut = context =>
        {
            Record(
                context.HttpContext,
                "oidc.remote-signout",
                "Received a remote sign-out notification from Keylet");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            Record(
                context.HttpContext,
                "oidc.authentication-failed",
                "OIDC authentication failed",
                new Dictionary<string, string?>
                {
                    ["exception"] = context.Exception.GetType().Name
                });
            return Task.CompletedTask;
        },
        OnRemoteFailure = context =>
        {
            Record(
                context.HttpContext,
                "oidc.remote-failure",
                "The remote OIDC flow failed",
                new Dictionary<string, string?>
                {
                    ["failure"] = context.Failure?.GetType().Name
                });
            return Task.CompletedTask;
        }
    };

    private static void Record(
        HttpContext context,
        string type,
        string summary,
        IReadOnlyDictionary<string, string?>? properties = null) =>
        context.RequestServices.GetRequiredService<IClientEventStore>()
            .Record(type, summary, properties);
}
