using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Keylet.Tests;

[TestClass]
public sealed class TestClientPageTests(TestContext testContext)
{
    [TestMethod]
    [Timeout(30_000)]
    public async Task HomePage_AnonymousSession_ShowsSafeLoginAndEventSurfaces()
    {
        await using var factory = new KeyletTestClientApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://client.test")
        });

        using var response = await client.GetAsync("/", testContext.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(testContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sign in with Keylet", html);
        Assert.Contains("Client events", html);
        Assert.Contains("client.started", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains("https://keylet.test/events", html);
        Assert.DoesNotContain(KeyletTestClientApplicationFactory.ClientSecret, html);
    }
}

internal sealed class KeyletTestClientApplicationFactory
    : WebApplicationFactory<Keylet.TestClient.Program>
{
    public const string ClientSecret = "never-render-this-test-secret";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development")
            .UseSetting("Authentication:Keylet:Authority", "https://keylet.test")
            .UseSetting("Authentication:Keylet:ClientId", "test-client-page-tests")
            .UseSetting("Authentication:Keylet:ClientSecret", ClientSecret)
            .UseSetting("Authentication:Keylet:RequireHttpsMetadata", "true");
    }
}
