using System.Collections.Immutable;
using System.Security.Claims;
using Keylet.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Keylet.Authorization;

internal static class KeyletClaims
{
    public static ClaimsPrincipal CreatePrincipal(KeyletUserOptions user, IEnumerable<string> scopes)
    {
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        identity.SetClaim(OpenIddictConstants.Claims.Subject, user.Subject);
        identity.SetClaim(OpenIddictConstants.Claims.Name, user.Name);
        identity.SetClaim(
            OpenIddictConstants.Claims.PreferredUsername,
            user.PreferredUsername ?? user.Email ?? user.Subject);

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            identity.SetClaim(OpenIddictConstants.Claims.Email, user.Email);
            identity.AddClaim(new Claim(
                OpenIddictConstants.Claims.EmailVerified,
                user.EmailVerified.ToString().ToLowerInvariant(),
                ClaimValueTypes.Boolean));
        }

        identity.SetClaims(OpenIddictConstants.Claims.Role, user.Roles.ToImmutableArray());
        identity.SetScopes(scopes);
        identity.SetDestinations(GetDestinations);

        return new ClaimsPrincipal(identity);
    }

    private static IEnumerable<string> GetDestinations(Claim claim) => claim.Type switch
    {
        OpenIddictConstants.Claims.Subject =>
        [
            OpenIddictConstants.Destinations.AccessToken,
            OpenIddictConstants.Destinations.IdentityToken
        ],

        OpenIddictConstants.Claims.Name or OpenIddictConstants.Claims.PreferredUsername
            when claim.Subject?.HasScope(OpenIddictConstants.Scopes.Profile) is true =>
        [
            OpenIddictConstants.Destinations.AccessToken,
            OpenIddictConstants.Destinations.IdentityToken
        ],

        OpenIddictConstants.Claims.Email or OpenIddictConstants.Claims.EmailVerified
            when claim.Subject?.HasScope(OpenIddictConstants.Scopes.Email) is true =>
        [
            OpenIddictConstants.Destinations.AccessToken,
            OpenIddictConstants.Destinations.IdentityToken
        ],

        OpenIddictConstants.Claims.Role
            when claim.Subject?.HasScope(OpenIddictConstants.Scopes.Roles) is true =>
        [
            OpenIddictConstants.Destinations.AccessToken,
            OpenIddictConstants.Destinations.IdentityToken
        ],

        _ => []
    };
}

