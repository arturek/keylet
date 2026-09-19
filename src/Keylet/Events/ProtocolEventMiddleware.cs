using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore;
using OpenIddict.Server.AspNetCore;

namespace Keylet.Events;

internal sealed class ProtocolEventMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IKeyletEventStore events)
    {
        await next(context);

        var type = GetEventType(context.Request.Path);
        if (type is null)
        {
            return;
        }

        var request = context.GetOpenIddictServerRequest();
        events.Record(
            type,
            $"{context.Request.Method} {context.Request.Path} returned {context.Response.StatusCode}",
            new Dictionary<string, string?>
            {
                ["client_id"] = request?.ClientId,
                ["grant_type"] = request?.GrantType,
                ["status_code"] = context.Response.StatusCode.ToString()
            });
    }

    private static string? GetEventType(PathString path) => path.Value switch
    {
        "/.well-known/openid-configuration" => "protocol.discovery",
        "/.well-known/jwks" => "protocol.jwks",
        "/connect/authorize" => "protocol.authorization",
        "/connect/token" => "protocol.token",
        "/connect/userinfo" => "protocol.userinfo",
        "/connect/logout" => "protocol.logout",
        _ => null
    };
}
