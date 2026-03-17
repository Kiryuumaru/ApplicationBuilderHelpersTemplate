using Application.Cloud.Node;
using Infrastructure.InMemory;
using Infrastructure.OpenTelemetry;
using Presentation.Cli.Commands;

return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
    .AddApplication<Domain.Domain>()
    .AddApplication<Application.Application>()
    .AddApplication<CloudNodeApplication>()
    .AddApplication<InMemoryInfrastructure>()
    .AddApplication<OpenTelemetryInfrastructure>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
