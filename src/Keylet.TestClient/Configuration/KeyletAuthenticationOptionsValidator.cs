using Microsoft.Extensions.Options;

namespace Keylet.TestClient.Configuration;

internal sealed class KeyletAuthenticationOptionsValidator : IValidateOptions<KeyletAuthenticationOptions>
{
    public ValidateOptionsResult Validate(string? name, KeyletAuthenticationOptions options)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority)
            || (options.RequireHttpsMetadata
                && !Uri.UriSchemeHttps.Equals(authority.Scheme, StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add("Authentication:Keylet:Authority must be an absolute HTTPS URL when RequireHttpsMetadata is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            failures.Add("Authentication:Keylet:ClientId is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            failures.Add("Authentication:Keylet:ClientSecret is required.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
