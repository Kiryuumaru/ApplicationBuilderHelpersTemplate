using Application.Client;
using Application.Common.Interfaces.Application;
using Infrastructure.Browser.IndexedDB.LocalStore;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Presentation.WebApp.Client.Commands;

try
{
    return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
        .AddApplication<ClientApplication>()
        .AddApplication<IndexedDBLocalStoreInfrastructure>()
        .AddCommand<MainCommand>()
        .RunAsync(args);

}
catch (Exception ex)
{
    Console.Error.WriteLine($"An error occurred: {ex.Message}, StackTrace: {ex.StackTrace}");
    return -1;
}
