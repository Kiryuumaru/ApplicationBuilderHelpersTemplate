using Application;
using Application.Logger.Extensions;
using ApplicationBuilderHelpers;
using Infrastructure.SQLite.LocalStore;
using Presentation.Commands;

return await ApplicationBuilder.Create()
    .AddCommand<MainCommand>()
    .AddApplication<Application.Application>()
    .AddApplication<SQLiteLocalStoreInfrastructure>()
    .RunAsync(args);
