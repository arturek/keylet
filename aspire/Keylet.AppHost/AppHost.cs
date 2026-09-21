using Aspire.Hosting.ApplicationModel;
using Keylet.Hosting.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

var useContainerValue = builder.Configuration["Parameters:keylet-use-container"] ?? "true";
builder.AddParameter(
    "keylet-use-container",
    useContainerValue,
    publishValueAsDefault: true);
if (!bool.TryParse(useContainerValue, out var useContainer))
{
    throw new InvalidOperationException(
        $"The Parameters:keylet-use-container value '{useContainerValue}' must be true or false.");
}

if (useContainer)
{
    var keylet = builder.AddKeylet("keylet");
    AddTestClient(keylet, endpointName: "http", requireHttpsMetadata: false);
}
else
{
    var keylet = builder.AddProject<Projects.Keylet>("keylet")
        .WithExternalHttpEndpoints();
    AddTestClient(keylet, endpointName: "https", requireHttpsMetadata: true);
}

void AddTestClient<T>(
    IResourceBuilder<T> keylet,
    string endpointName,
    bool requireHttpsMetadata)
    where T : IResourceWithEnvironment, IResourceWithEndpoints
{
    var testClient = builder.AddProject<Projects.Keylet_TestClient>("test-client")
        .WithExternalHttpEndpoints()
        .WithKeyletAuthentication(
            keylet,
            "keylet-test-client",
            "keylet-test-client-secret",
            endpointName: endpointName,
            requireHttpsMetadata: requireHttpsMetadata)
        .WaitFor(builder.CreateResourceBuilder<IResource>(keylet.Resource));

    keylet.WithKeyletClient(
        testClient,
        clientId: "keylet-test-client",
        clientSecret: "keylet-test-client-secret",
        clientIndex: 1);
}

builder.Build().Run();
