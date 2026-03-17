using Application.Cloud.Router;
using Infrastructure.InMemory;
using Infrastructure.OpenTelemetry;
using Presentation.Cli.Commands;

return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
    .AddApplication<Domain.Domain>()
    .AddApplication<Application.Application>()
    .AddApplication<CloudRouterApplication>()
    .AddApplication<InMemoryInfrastructure>()
    .AddApplication<OpenTelemetryInfrastructure>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
