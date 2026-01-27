using Presentation.Cli.Commands;

return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
    .AddApplication<Domain.Domain>()
    .AddApplication<Application.Application>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
