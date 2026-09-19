using System.Security.Claims;
using Keylet.Configuration;
using Keylet.Events;
using Keylet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Microsoft.AspNetCore.Routing;

internal static class OpenIddictEndpointRouteBuilderExtensions
{
    public const string CookieScheme = "Keylet.Session";

    public static IEndpointRouteBuilder MapKeyletOpenIddictEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], (Delegate)AuthorizeAsync);
        endpoints.MapMethods("/connect/logout", [HttpMethods.Get, HttpMethods.Post], (Delegate)LogoutAsync);
        endpoints.MapMethods("/connect/userinfo", [HttpMethods.Get, HttpMethods.Post], (Delegate)UserInfoAsync);
        return endpoints;
    }

    private static async Task<IResult> AuthorizeAsync(
        HttpContext context,
        IConfiguredUserStore users,
        IOptions<KeyletOptions> options,
        IKeyletEventStore events)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect authorization request was not available.");

        KeyletUserOptions? user;
        if (options.Value.Mode == KeyletMode.Automatic)
        {
            user = users.GetAutomaticUser(request.LoginHint);
            events.Record(
                "account.auto-selected",
                $"Automatically selected {user.Subject}",
                new Dictionary<string, string?>
                {
                    ["client_id"] = request.ClientId,
                    ["subject"] = user.Subject,
                    ["login_hint"] = request.LoginHint
                });
        }
        else
        {
            var result = await context.AuthenticateAsync(CookieScheme);
            user = result.Succeeded
                ? users.Find(result.Principal?.FindFirstValue(OpenIddictConstants.Claims.Subject))
                : null;

            if (user is null)
            {
                return Results.Challenge(
                    new AuthenticationProperties
                    {
                        RedirectUri = context.Request.PathBase + context.Request.Path + context.Request.QueryString
                    },
                    [CookieScheme]);
            }
        }

        return Results.SignIn(
            Keylet.Authorization.KeyletClaims.CreatePrincipal(user, request.GetScopes()),
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, IKeyletEventStore events)
    {
        var subject = context.User.FindFirstValue(OpenIddictConstants.Claims.Subject);
        await context.SignOutAsync(CookieScheme);
        events.Record(
            "account.signed-out",
            "The local Keylet session was cleared",
            new Dictionary<string, string?> { ["subject"] = subject });

        return Results.SignOut(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static async Task<IResult> UserInfoAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            return Results.Challenge(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var principal = result.Principal;
        var response = new Dictionary<string, object?>
        {
            [OpenIddictConstants.Claims.Subject] = principal.GetClaim(OpenIddictConstants.Claims.Subject)
        };

        if (principal.HasScope(OpenIddictConstants.Scopes.Profile))
        {
            response[OpenIddictConstants.Claims.Name] = principal.GetClaim(OpenIddictConstants.Claims.Name);
            response[OpenIddictConstants.Claims.PreferredUsername] = principal.GetClaim(OpenIddictConstants.Claims.PreferredUsername);
        }

        if (principal.HasScope(OpenIddictConstants.Scopes.Email))
        {
            response[OpenIddictConstants.Claims.Email] = principal.GetClaim(OpenIddictConstants.Claims.Email);
            response[OpenIddictConstants.Claims.EmailVerified] =
                principal.GetClaim(OpenIddictConstants.Claims.EmailVerified) is "true";
        }

        if (principal.HasScope(OpenIddictConstants.Scopes.Roles))
        {
            response[OpenIddictConstants.Claims.Role] = principal.GetClaims(OpenIddictConstants.Claims.Role);
        }

        return Results.Ok(response);
    }
}
