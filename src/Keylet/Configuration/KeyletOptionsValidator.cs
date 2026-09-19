using Microsoft.Extensions.Options;

namespace Keylet.Configuration;

internal sealed class KeyletOptionsValidator : IValidateOptions<KeyletOptions>
{
    public ValidateOptionsResult Validate(string? name, KeyletOptions options)
    {
        var failures = new List<string>();

        if (options.EventCapacity is < 1 or > 10_000)
        {
            failures.Add("Keylet:EventCapacity must be between 1 and 10000.");
        }

        if (!string.IsNullOrWhiteSpace(options.Issuer))
        {
            if (!Uri.TryCreate(options.Issuer, UriKind.Absolute, out var issuer)
                || (!options.AllowInsecureHttp && !Uri.UriSchemeHttps.Equals(issuer.Scheme, StringComparison.OrdinalIgnoreCase))
                || !string.IsNullOrEmpty(issuer.Query)
                || !string.IsNullOrEmpty(issuer.Fragment))
            {
                failures.Add("Keylet:Issuer must be an absolute URL without a query or fragment, and must use HTTPS unless AllowInsecureHttp is enabled.");
            }
        }

        ValidateUsers(options, failures);
        ValidateClients(options, failures);

        if (options.Mode == KeyletMode.Automatic
            && FindUser(options.Users, options.AutomaticUser) is null)
        {
            failures.Add("Keylet:AutomaticUser must identify a configured user when Mode is Automatic.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateUsers(KeyletOptions options, List<string> failures)
    {
        if (options.Users.Count == 0)
        {
            failures.Add("At least one Keylet user must be configured.");
            return;
        }

        foreach (var user in options.Users)
        {
            if (string.IsNullOrWhiteSpace(user.Subject))
            {
                failures.Add("Every Keylet user requires a non-empty Subject.");
            }

            if (string.IsNullOrWhiteSpace(user.Name))
            {
                failures.Add($"Keylet user '{user.Subject}' requires a non-empty Name.");
            }
        }

        var duplicateSubjects = options.Users
            .GroupBy(user => user.Subject, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        foreach (var subject in duplicateSubjects)
        {
            failures.Add($"Keylet user Subject '{subject}' is configured more than once.");
        }
    }

    private static void ValidateClients(KeyletOptions options, List<string> failures)
    {
        if (options.Clients.Count == 0)
        {
            failures.Add("At least one Keylet client must be configured.");
            return;
        }

        foreach (var client in options.Clients)
        {
            if (string.IsNullOrWhiteSpace(client.ClientId))
            {
                failures.Add("Every Keylet client requires a non-empty ClientId.");
            }

            if (client.RedirectUris.Count == 0)
            {
                failures.Add($"Keylet client '{client.ClientId}' requires at least one RedirectUri.");
            }

            ValidateUris(client.ClientId, "RedirectUris", client.RedirectUris, failures);
            ValidateUris(client.ClientId, "PostLogoutRedirectUris", client.PostLogoutRedirectUris, failures);

            if (!client.AllowedScopes.Contains("openid", StringComparer.Ordinal))
            {
                failures.Add($"Keylet client '{client.ClientId}' must allow the 'openid' scope.");
            }
        }

        var duplicateClientIds = options.Clients
            .GroupBy(client => client.ClientId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        foreach (var clientId in duplicateClientIds)
        {
            failures.Add($"Keylet client ClientId '{clientId}' is configured more than once.");
        }
    }

    private static void ValidateUris(
        string clientId,
        string optionName,
        IEnumerable<string> values,
        List<string> failures)
    {
        foreach (var value in values)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.Fragment))
            {
                failures.Add($"Keylet client '{clientId}' has an invalid {optionName} value '{value}'.");
            }
        }
    }

    private static KeyletUserOptions? FindUser(IEnumerable<KeyletUserOptions> users, string? identifier) =>
        users.FirstOrDefault(user =>
            string.Equals(user.Subject, identifier, StringComparison.Ordinal)
            || string.Equals(user.PreferredUsername, identifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Email, identifier, StringComparison.OrdinalIgnoreCase));
}

