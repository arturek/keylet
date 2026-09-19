var builder = DistributedApplication.CreateBuilder(args);

var keylet = builder.AddProject<Projects.Keylet>("keylet")
    .WithExternalHttpEndpoints();

var testClient = builder.AddProject<Projects.Keylet_TestClient>("test-client")
    .WithExternalHttpEndpoints()
    .WithEnvironment("Authentication__Keylet__Authority", keylet.GetEndpoint("https"))
    .WaitFor(keylet);

keylet
    .WithEnvironment(
        "Keylet__Clients__1__RedirectUris__0",
        $"{testClient.GetEndpoint("https")}/signin-oidc")
    .WithEnvironment(
        "Keylet__Clients__1__PostLogoutRedirectUris__0",
        $"{testClient.GetEndpoint("https")}/signout-callback-oidc");

builder.Build().Run();
