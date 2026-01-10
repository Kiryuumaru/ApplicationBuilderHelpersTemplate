var builder = DistributedApplication.CreateBuilder(args);

var webapi = builder.AddProject<Projects.Presentation_WebApi>("webapi")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Presentation_WebApp>("webapp")
    .WithExternalHttpEndpoints()
    .WithReference(webapi)
    .WaitFor(webapi);

builder.Build().Run();
