using Keylet.Configuration;
using Keylet.Data;
using Keylet.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Keylet.Services;

internal sealed class KeyletConfigurationSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<KeyletOptions> options,
    IKeyletEventStore events)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<KeyletDbContext>();
        await database.Database.EnsureCreatedAsync(cancellationToken);

        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        foreach (var client in options.Value.Clients)
        {
            var existing = await applications.FindByClientIdAsync(client.ClientId, cancellationToken);
            if (existing is not null)
            {
                await applications.DeleteAsync(existing, cancellationToken);
            }

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ApplicationType = OpenIddictConstants.ApplicationTypes.Web,
                ClientId = client.ClientId,
                ClientSecret = string.IsNullOrWhiteSpace(client.ClientSecret) ? null : client.ClientSecret,
                ClientType = string.IsNullOrWhiteSpace(client.ClientSecret)
                    ? OpenIddictConstants.ClientTypes.Public
                    : OpenIddictConstants.ClientTypes.Confidential,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                DisplayName = client.DisplayName ?? client.ClientId
            };

            foreach (var uri in client.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri));
            }

            foreach (var uri in client.PostLogoutRedirectUris)
            {
                descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
            }

            descriptor.Permissions.UnionWith(
            [
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode
            ]);

            if (client.AllowRefreshTokens)
            {
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
                descriptor.Permissions.Add(
                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess);
            }

            foreach (var allowedScope in client.AllowedScopes)
            {
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + allowedScope);
            }

            if (client.RequirePkce)
            {
                descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
            }

            await applications.CreateAsync(descriptor, cancellationToken);
        }

        events.Record(
            "configuration.loaded",
            "Configured users and clients were loaded",
            new Dictionary<string, string?>
            {
                ["mode"] = options.Value.Mode.ToString(),
                ["users"] = options.Value.Users.Count.ToString(),
                ["clients"] = options.Value.Clients.Count.ToString()
            });
    }
}
