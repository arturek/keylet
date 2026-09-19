using Keylet.Configuration;
using Microsoft.Extensions.Options;

namespace Keylet.Services;

public interface IConfiguredUserStore
{
    IReadOnlyList<KeyletUserOptions> GetAll();

    KeyletUserOptions? Find(string? identifier);

    KeyletUserOptions GetAutomaticUser(string? loginHint);
}

internal sealed class ConfiguredUserStore(IOptions<KeyletOptions> options) : IConfiguredUserStore
{
    public IReadOnlyList<KeyletUserOptions> GetAll() => options.Value.Users;

    public KeyletUserOptions? Find(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        return options.Value.Users.FirstOrDefault(user =>
            string.Equals(user.Subject, identifier, StringComparison.Ordinal)
            || string.Equals(user.PreferredUsername, identifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Email, identifier, StringComparison.OrdinalIgnoreCase));
    }

    public KeyletUserOptions GetAutomaticUser(string? loginHint)
    {
        var configured = options.Value;
        var selected = configured.AllowLoginHint ? Find(loginHint) : null;
        return selected
            ?? Find(configured.AutomaticUser)
            ?? throw new InvalidOperationException("The automatic Keylet user is not configured.");
    }
}

