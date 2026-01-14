using Application.Client;
using Presentation.WebApp.Client.Controllers;

return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
    .AddApplication<ClientApplication>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
