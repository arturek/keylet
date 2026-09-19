using Keylet.TestClient.Configuration;
using Keylet.TestClient.Events;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Keylet.TestClient.Pages;

public sealed class IndexModel(
    IClientEventStore events,
    IOptions<KeyletAuthenticationOptions> authentication) : PageModel
{
    public bool IsAuthenticated { get; private set; }

    public string? DisplayName { get; private set; }

    public string? Subject { get; private set; }

    public string? Email { get; private set; }

    public IReadOnlyList<string> Roles { get; private set; } = [];

    public IReadOnlyList<ClaimDetails> Claims { get; private set; } = [];

    public ClientSessionDetails? Session { get; private set; }

    public IReadOnlyList<ClientEvent> Events { get; private set; } = [];

    public string KeyletEventsUrl => new Uri(
        new Uri(authentication.Value.Authority.TrimEnd('/') + "/"),
        "events").AbsoluteUri;

    public async Task OnGetAsync()
    {
        IsAuthenticated = User.Identity?.IsAuthenticated is true;
        DisplayName = User.Identity?.Name;
        Subject = User.FindFirst("sub")?.Value;
        Email = User.FindFirst("email")?.Value;
        Roles = User.FindAll("role").Select(claim => claim.Value).Distinct(StringComparer.Ordinal).ToArray();
        Claims = User.Claims
            .OrderBy(claim => claim.Type, StringComparer.Ordinal)
            .ThenBy(claim => claim.Value, StringComparer.Ordinal)
            .Select(claim => new ClaimDetails(claim.Type, claim.Value, claim.Issuer))
            .ToArray();

        var cookie = await HttpContext.AuthenticateAsync(Program.CookieScheme);
        if (cookie.Succeeded)
        {
            Session = new ClientSessionDetails(
                cookie.Properties?.IssuedUtc,
                cookie.Properties?.ExpiresUtc,
                cookie.Properties?.IsPersistent ?? false);
        }

        Events = events.GetAll();
    }

    public IActionResult OnPostLogin()
    {
        events.Record("ui.login-requested", "The user selected Sign in with Keylet");
        return Challenge(
            new AuthenticationProperties { RedirectUri = Url.Page("/Index") },
            Program.OpenIdConnectScheme);
    }

    public IActionResult OnPostLogout()
    {
        events.Record(
            "ui.logout-requested",
            "The user selected Sign out through Keylet",
            new Dictionary<string, string?> { ["subject"] = User.FindFirst("sub")?.Value });

        return SignOut(
            new AuthenticationProperties { RedirectUri = Url.Page("/Index") },
            Program.CookieScheme,
            Program.OpenIdConnectScheme);
    }

    public sealed record ClaimDetails(string Type, string Value, string Issuer);

    public sealed record ClientSessionDetails(
        DateTimeOffset? IssuedUtc,
        DateTimeOffset? ExpiresUtc,
        bool IsPersistent);
}

