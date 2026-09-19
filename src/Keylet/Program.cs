using Keylet.Configuration;
using Keylet.Data;
using Keylet.Events;
using Keylet.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddRazorPages();

builder.Services.AddSingleton<IValidateOptions<KeyletOptions>, KeyletOptionsValidator>();
builder.Services.AddOptions<KeyletOptions>()
    .BindConfiguration(KeyletOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IConfiguredUserStore, ConfiguredUserStore>();
builder.Services.AddSingleton<IKeyletEventStore, KeyletEventStore>();
builder.Services.AddSingleton<KeyletConfigurationSeeder>();

builder.Services.AddAuthentication(OpenIddictEndpointRouteBuilderExtensions.CookieScheme)
    .AddCookie(OpenIddictEndpointRouteBuilderExtensions.CookieScheme, options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "Keylet.Session";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.LoginPath = "/account/login";
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();

var configuredOptions = builder.Configuration
    .GetRequiredSection(KeyletOptions.SectionName)
    .Get<KeyletOptions>()
    ?? throw new InvalidOperationException("Keylet configuration was not provided.");

var databaseName = $"keylet-{Guid.NewGuid():N}";
builder.Services.AddDbContext<KeyletDbContext>(options =>
{
    options.UseInMemoryDatabase(databaseName);
    options.UseOpenIddict();
});

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<KeyletDbContext>())
    .AddServer(options =>
    {
        if (!string.IsNullOrWhiteSpace(configuredOptions.Issuer))
        {
            options.SetIssuer(new Uri(configuredOptions.Issuer));
        }

        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetUserInfoEndpointUris("/connect/userinfo")
            .SetEndSessionEndpointUris("/connect/logout")
            .AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange()
            .AddEphemeralEncryptionKey()
            .AddEphemeralSigningKey();

        if (configuredOptions.Clients.Any(client => client.AllowRefreshTokens))
        {
            options.AllowRefreshTokenFlow();
        }

        var scopes = configuredOptions.Clients
            .SelectMany(client => client.AllowedScopes)
            .Where(scope => scope is not OpenIddictConstants.Scopes.OpenId
                and not OpenIddictConstants.Scopes.OfflineAccess)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (scopes.Length > 0)
        {
            options.RegisterScopes(scopes);
        }

        var aspNetCore = options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough()
            .EnableUserInfoEndpointPassthrough();

        if (configuredOptions.AllowInsecureHttp)
        {
            aspNetCore.DisableTransportSecurityRequirement();
        }
    });

var app = builder.Build();

_ = app.Services.GetRequiredService<IOptions<KeyletOptions>>().Value;
await app.Services.GetRequiredService<KeyletConfigurationSeeder>().SeedAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!configuredOptions.AllowInsecureHttp)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<ProtocolEventMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapKeyletOpenIddictEndpoints();
app.MapDefaultEndpoints();

await app.RunAsync();

public partial class Program;
