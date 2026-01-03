using ApplicationBuilderHelpers;
using Presentation.Cli.Commands;

return await ApplicationBuilder.Create()
    .AddApplication<Application.Application>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
