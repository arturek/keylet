using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace Keylet.Tests;

[TestClass]
public sealed class OpenIddictProtocolTests(TestContext testContext)
{
    [TestMethod]
    [Timeout(30_000)]
    public async Task AuthorizationCodeFlow_InteractiveSelection_IssuesExpectedIdentity()
    {
        await using var factory = new KeyletWebApplicationFactory(automatic: false);
        using var client = factory.CreateProtocolClient();

        var authorization = await CompleteAuthorizationAsync(
            client,
            factory,
            loginHint: null,
            selectSubject: "admin",
            testContext.CancellationToken);
        using var tokens = await ExchangeCodeAsync(
            client,
            factory,
            authorization,
            testContext.CancellationToken);

        var validation = await ValidateIdentityTokenAsync(
            client,
            tokens.RootElement.GetProperty("id_token").GetString(),
            factory,
            testContext.CancellationToken);

        Assert.IsTrue(validation.IsValid, validation.Exception?.ToString());
        Assert.AreEqual("admin", validation.ClaimsIdentity?.FindFirst(OpenIddictConstants.Claims.Subject)?.Value);
        Assert.AreEqual("admin@keylet.test", validation.ClaimsIdentity?.FindFirst(OpenIddictConstants.Claims.Email)?.Value);
        Assert.Contains(
            "Admin",
            validation.ClaimsIdentity?.FindAll(OpenIddictConstants.Claims.Role).Select(claim => claim.Value).ToArray() ?? []);

        using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        userInfoRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokens.RootElement.GetProperty("access_token").GetString());
        using var userInfoResponse = await client.SendAsync(userInfoRequest, testContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, userInfoResponse.StatusCode);
        using var userInfo = JsonDocument.Parse(await userInfoResponse.Content.ReadAsStringAsync(testContext.CancellationToken));
        Assert.AreEqual("admin", userInfo.RootElement.GetProperty(OpenIddictConstants.Claims.Subject).GetString());
        Assert.AreEqual("admin@keylet.test", userInfo.RootElement.GetProperty(OpenIddictConstants.Claims.Email).GetString());

        using var eventsPage = await client.GetAsync("/events", testContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, eventsPage.StatusCode);
        var eventsHtml = await eventsPage.Content.ReadAsStringAsync(testContext.CancellationToken);
        Assert.Contains("account.selected", eventsHtml);
        Assert.Contains("protocol.authorization", eventsHtml);
        Assert.Contains("protocol.token", eventsHtml);
        Assert.Contains("protocol.userinfo", eventsHtml);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task AuthorizationCodeFlow_AutomaticModeHonorsLoginHint_WithoutLoginPage()
    {
        await using var factory = new KeyletWebApplicationFactory(automatic: true);
        using var client = factory.CreateProtocolClient();

        var authorization = await CompleteAuthorizationAsync(
            client,
            factory,
            loginHint: "user",
            selectSubject: null,
            testContext.CancellationToken);
        using var tokens = await ExchangeCodeAsync(
            client,
            factory,
            authorization,
            testContext.CancellationToken);
        var validation = await ValidateIdentityTokenAsync(
            client,
            tokens.RootElement.GetProperty("id_token").GetString(),
            factory,
            testContext.CancellationToken);

        Assert.IsTrue(validation.IsValid, validation.Exception?.ToString());
        Assert.AreEqual("user", validation.ClaimsIdentity?.FindFirst(OpenIddictConstants.Claims.Subject)?.Value);

        using var eventsPage = await client.GetAsync("/events", testContext.CancellationToken);
        var eventsHtml = await eventsPage.Content.ReadAsStringAsync(testContext.CancellationToken);
        Assert.Contains("account.auto-selected", eventsHtml);
        Assert.Contains("login_hint", eventsHtml);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task AuthorizationRequest_WithUnregisteredRedirectUri_IsRejected()
    {
        await using var factory = new KeyletWebApplicationFactory(automatic: true);
        using var client = factory.CreateProtocolClient();

        using var response = await client.GetAsync(
            CreateAuthorizationUri(factory, "https://untrusted.test/callback", loginHint: null).Uri,
            testContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_request", await response.Content.ReadAsStringAsync(testContext.CancellationToken));
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task HomePage_DoesNotRenderConfiguredClientSecret()
    {
        await using var factory = new KeyletWebApplicationFactory(automatic: false);
        using var client = factory.CreateProtocolClient();

        var html = await client.GetStringAsync("/", testContext.CancellationToken);

        Assert.Contains(factory.ClientId, html);
        Assert.DoesNotContain(factory.ClientSecret, html);
    }

    private static async Task<AuthorizationCode> CompleteAuthorizationAsync(
        HttpClient client,
        KeyletWebApplicationFactory factory,
        string? loginHint,
        string? selectSubject,
        CancellationToken cancellationToken)
    {
        var authorization = CreateAuthorizationUri(factory, factory.RedirectUri.AbsoluteUri, loginHint);
        using var challenge = await client.GetAsync(authorization.Uri, cancellationToken);
        Assert.AreEqual(HttpStatusCode.Found, challenge.StatusCode);
        Assert.IsNotNull(challenge.Headers.Location);

        Uri clientCallback;
        if (selectSubject is null)
        {
            clientCallback = challenge.Headers.Location;
        }
        else
        {
            Assert.AreEqual("/account/login", challenge.Headers.Location.AbsolutePath);
            using var loginPage = await client.GetAsync(challenge.Headers.Location, cancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, loginPage.StatusCode);
            var loginHtml = await loginPage.Content.ReadAsStringAsync(cancellationToken);
            var antiforgeryToken = GetAntiforgeryToken(loginHtml);
            var returnUrl = QueryHelpers.ParseQuery(challenge.Headers.Location.Query)["ReturnUrl"].ToString();

            using var loginResponse = await client.PostAsync(
                challenge.Headers.Location,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = antiforgeryToken,
                    ["ReturnUrl"] = returnUrl,
                    ["Subject"] = selectSubject
                }),
                cancellationToken);
            Assert.AreEqual(HttpStatusCode.Found, loginResponse.StatusCode);
            Assert.IsNotNull(loginResponse.Headers.Location);

            using var resumedAuthorization = await client.GetAsync(loginResponse.Headers.Location, cancellationToken);
            Assert.AreEqual(HttpStatusCode.Found, resumedAuthorization.StatusCode);
            Assert.IsNotNull(resumedAuthorization.Headers.Location);
            clientCallback = resumedAuthorization.Headers.Location;
        }

        Assert.StartsWith(factory.RedirectUri.AbsoluteUri, clientCallback.AbsoluteUri);
        var parameters = QueryHelpers.ParseQuery(clientCallback.Query);
        Assert.AreEqual("protocol-state", parameters["state"].ToString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(parameters["code"]));

        return new AuthorizationCode(parameters["code"].ToString(), authorization.CodeVerifier);
    }

    private static async Task<JsonDocument> ExchangeCodeAsync(
        HttpClient client,
        KeyletWebApplicationFactory factory,
        AuthorizationCode authorization,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = factory.ClientId,
                ["client_secret"] = factory.ClientSecret,
                ["code"] = authorization.Code,
                ["code_verifier"] = authorization.CodeVerifier,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = factory.RedirectUri.AbsoluteUri
            }),
            cancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static async Task<TokenValidationResult> ValidateIdentityTokenAsync(
        HttpClient client,
        string? identityToken,
        KeyletWebApplicationFactory factory,
        CancellationToken cancellationToken)
    {
        Assert.IsNotNull(identityToken);
        using var discovery = JsonDocument.Parse(await client.GetStringAsync(
            "/.well-known/openid-configuration",
            cancellationToken));
        var issuer = discovery.RootElement.GetProperty("issuer").GetString();
        var jwksUri = discovery.RootElement.GetProperty("jwks_uri").GetString();
        Assert.IsNotNull(issuer);
        Assert.IsNotNull(jwksUri);

        var keys = new JsonWebKeySet(await client.GetStringAsync(jwksUri, cancellationToken)).GetSigningKeys();
        return await new JsonWebTokenHandler().ValidateTokenAsync(identityToken, new TokenValidationParameters
        {
            IssuerSigningKeys = keys,
            ValidAudience = factory.ClientId,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateLifetime = true
        });
    }

    private static AuthorizationRequest CreateAuthorizationUri(
        KeyletWebApplicationFactory factory,
        string redirectUri,
        string? loginHint)
    {
        var codeVerifier = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var codeChallenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = factory.ClientId,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["login_hint"] = loginHint,
            ["nonce"] = "protocol-nonce",
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid profile email roles",
            ["state"] = "protocol-state"
        };

        return new AuthorizationRequest(QueryHelpers.AddQueryString("/connect/authorize", parameters), codeVerifier);
    }

    private static string GetAntiforgeryToken(string page) => Regex.Match(
        page,
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
        RegexOptions.CultureInvariant).Groups["token"].Value;

    private sealed record AuthorizationRequest(string Uri, string CodeVerifier);

    private sealed record AuthorizationCode(string Code, string CodeVerifier);
}

internal sealed class KeyletWebApplicationFactory(bool automatic) : WebApplicationFactory<global::Program>
{
    public string ClientId { get; } = "keylet-protocol-tests";

    public string ClientSecret { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public Uri RedirectUri { get; } = new("https://client.test/signin-oidc");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development")
            .UseSetting("Keylet:Issuer", "https://keylet.test")
            .UseSetting("Keylet:Mode", automatic ? "Automatic" : "Interactive")
            .UseSetting("Keylet:AutomaticUser", "admin")
            .UseSetting("Keylet:Users:0:Subject", "admin")
            .UseSetting("Keylet:Users:0:Name", "Test Administrator")
            .UseSetting("Keylet:Users:0:PreferredUsername", "admin")
            .UseSetting("Keylet:Users:0:Email", "admin@keylet.test")
            .UseSetting("Keylet:Users:0:Roles:0", "Admin")
            .UseSetting("Keylet:Users:1:Subject", "user")
            .UseSetting("Keylet:Users:1:Name", "Test User")
            .UseSetting("Keylet:Users:1:PreferredUsername", "user")
            .UseSetting("Keylet:Users:1:Email", "user@keylet.test")
            .UseSetting("Keylet:Users:1:Roles:0", "User")
            .UseSetting("Keylet:Clients:0:ClientId", ClientId)
            .UseSetting("Keylet:Clients:0:ClientSecret", ClientSecret)
            .UseSetting("Keylet:Clients:0:DisplayName", "Protocol test client")
            .UseSetting("Keylet:Clients:0:RedirectUris:0", RedirectUri.AbsoluteUri)
            .UseSetting("Keylet:Clients:0:PostLogoutRedirectUris:0", "https://client.test/signout-callback-oidc");
    }

    public HttpClient CreateProtocolClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://keylet.test")
    });
}
