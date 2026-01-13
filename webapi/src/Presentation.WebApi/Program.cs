using Infrastructure.EFCore;
using Infrastructure.EFCore.Identity;
using Infrastructure.EFCore.LocalStore;
using Infrastructure.EFCore.Sqlite;
using Infrastructure.Identity;
using Infrastructure.OpenTelemetry;
using Infrastructure.Passkeys;
using Presentation.WebApi.Commands;

return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
    .AddApplication<Application.Application>()
    .AddApplication<OpenTelemetryInfrastructure>()
    .AddApplication<IdentityInfrastructure>()
    .AddApplication<PasskeysInfrastructure>()
    .AddApplication<EFCoreInfrastructure>()
    .AddApplication<EFCoreSqliteInfrastructure>()
    .AddApplication<EFCoreIdentityInfrastructure>()
    .AddApplication<EFCoreLocalStoreInfrastructure>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
