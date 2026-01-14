using Application.Client;
using Presentation.WebApp.Client.Commands;

return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
    .AddApplication<ClientApplication>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
