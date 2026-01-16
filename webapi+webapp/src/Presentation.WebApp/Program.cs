using Application.Server;
using Infrastructure.EFCore;
using Infrastructure.EFCore.LocalStore;
using Infrastructure.EFCore.Server.Identity;
using Infrastructure.EFCore.Sqlite;
using Infrastructure.EFCore.Sqlite.Server;
using Infrastructure.OpenTelemetry;
using Infrastructure.Server.Identity;
using Infrastructure.Server.Passkeys;
using Presentation.WebApp.Commands;

return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
    .AddApplication<ServerApplication>()
    .AddApplication<OpenTelemetryInfrastructure>()
    .AddApplication<IdentityInfrastructure>()
    .AddApplication<PasskeysInfrastructure>()
    .AddApplication<EFCoreInfrastructure>()
    .AddApplication<EFCoreLocalStoreInfrastructure>()
    .AddApplication<EFCoreServerIdentityInfrastructure>()
    .AddApplication<EFCoreSqliteInfrastructure>()
    .AddApplication<EFCoreSqliteServerInfrastructure>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
