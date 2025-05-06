using Application;
using ApplicationBuilderHelpers;
using Infrastructure.Serilog;
using Infrastructure.SQLite.LocalStore;
using Presentation.Commands;

return await ApplicationBuilder.Create()
    .AddCommand<MainCommand>()
    .AddApplication<Application.Application>()
    .AddApplication<SerilogInfrastructure>()
    .AddApplication<SQLiteLocalStoreInfrastructure>()
    .RunAsync(args);
