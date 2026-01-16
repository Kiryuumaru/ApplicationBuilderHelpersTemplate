using Application.Client;
using Application.Common.Interfaces.Application;
using Infrastructure.EFCore;
using Infrastructure.EFCore.LocalStore;
using Infrastructure.EFCore.Sqlite;
using Infrastructure.EFCore.Sqlite.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Presentation.WebApp.Client.Commands;

try
{
    return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
        .AddApplication<ClientApplication>()
        .AddApplication<EFCoreInfrastructure>()
        .AddApplication<EFCoreLocalStoreInfrastructure>()
        .AddApplication<EFCoreSqliteInfrastructure>()
        .AddApplication<EFCoreSqliteClientInfrastructure>()
        .AddCommand<MainCommand>()
        .RunAsync(args);

}
catch (Exception ex)
{
    Console.Error.WriteLine($"An error occurred: {ex.Message}, StackTrace: {ex.StackTrace}");
    return -1;
}
