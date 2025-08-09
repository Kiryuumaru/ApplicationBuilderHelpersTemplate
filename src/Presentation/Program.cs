using ApplicationBuilderHelpers;
using Infrastructure.Serilog.Logger;
using Infrastructure.Sqlite.LocalStore;
using Presentation.Commands;

return await ApplicationBuilder.Create()
    .AddApplication<Application.Application>()
    .AddApplication<SerilogLoggerInfrastructure>()
    .AddApplication<SqliteLocalStoreInfrastructure>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
