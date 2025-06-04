using Application;
using Application.Logger.Extensions;
using ApplicationBuilderHelpers;
using Infrastructure.SQLite.LocalStore;
using Presentation.Commands;

return await ApplicationBuilder.Create()
    .AddApplication<Application.Application>()
    .AddApplication<SQLiteLocalStoreInfrastructure>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
