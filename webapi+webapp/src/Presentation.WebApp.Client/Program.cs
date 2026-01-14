using Application.Client;
using Application.Common.Interfaces.Application;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Presentation.WebApp.Client.Commands;

try
{
    return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
        .AddApplication<ClientApplication>()
        .AddCommand<MainCommand>()
        .RunAsync(args);

}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred: {ex.Message}, StackTrace: {ex.StackTrace}");
    return -1;
}
