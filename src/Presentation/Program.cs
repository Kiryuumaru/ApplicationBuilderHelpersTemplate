using Application;
using ApplicationBuilderHelpers;
using Infrastructure.Serilog;
using Presentation.Commands;

return await ApplicationBuilder.Create()
    .AddCommand<MainCommand>()
    .AddApplication<Application.Application>()
    .AddApplication<SerilogInfrastructure>()
    .RunAsync(args);
