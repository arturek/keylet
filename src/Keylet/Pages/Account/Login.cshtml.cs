using System.Security.Claims;
using Keylet.Events;
using Keylet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Keylet.Pages.Account;

public sealed class LoginModel(
    IConfiguredUserStore users,
    IKeyletEventStore events,
    IOpenIddictApplicationManager applications) : PageModel
{
    public IReadOnlyList<Configuration.KeyletUserOptions> Users => users.GetAll();

    public string? ClientName { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string Subject { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsSafeReturnUrl(ReturnUrl))
        {
            ReturnUrl = "/";
        }

        var clientId = TryGetClientId(ReturnUrl);
        var application = string.IsNullOrWhiteSpace(clientId)
            ? null
            : await applications.FindByClientIdAsync(clientId);
        ClientName = application is null
            ? clientId
            : await applications.GetDisplayNameAsync(application);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!IsSafeReturnUrl(ReturnUrl))
        {
            return BadRequest("The return URL is invalid.");
        }

        var user = users.Find(Subject);
        if (user is null)
        {
            return BadRequest("The selected Keylet identity does not exist.");
        }

        var identity = new ClaimsIdentity(
            OpenIddictEndpointRouteBuilderExtensions.CookieScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, user.Subject));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Name, user.Name));

        await HttpContext.SignInAsync(
            OpenIddictEndpointRouteBuilderExtensions.CookieScheme,
            new ClaimsPrincipal(identity));

        events.Record(
            "account.selected",
            $"Selected {user.Subject} from the interactive account picker",
            new Dictionary<string, string?>
            {
                ["subject"] = user.Subject,
                ["client_id"] = TryGetClientId(ReturnUrl)
            });

        return LocalRedirect(ReturnUrl!);
    }

    private bool IsSafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl);

    private static string? TryGetClientId(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        var queryIndex = returnUrl.IndexOf('?');
        if (queryIndex < 0)
        {
            return null;
        }

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers
            .ParseQuery(returnUrl[(queryIndex + 1)..])
            .GetValueOrDefault("client_id")
            .ToString();
    }
}
