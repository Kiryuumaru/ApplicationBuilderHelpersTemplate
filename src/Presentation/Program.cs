using Application;
using Application.Logger.Extensions;
using ApplicationBuilderHelpers;
using Presentation.Commands;

return await ApplicationBuilder.Create()
    .AddApplication<Application.Application>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
