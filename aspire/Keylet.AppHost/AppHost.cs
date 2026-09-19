var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Keylet>("keylet")
    .WithExternalHttpEndpoints();

builder.Build().Run();

