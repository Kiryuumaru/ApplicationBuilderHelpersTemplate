using Application.Server;
using Infrastructure.EFCore;
using Infrastructure.EFCore.Server.Identity;
using Infrastructure.EFCore.LocalStore;
using Infrastructure.EFCore.Sqlite;
using Infrastructure.Server.Identity;
using Infrastructure.Server.Passkeys;
using Presentation.WebApi.Commands;

return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
    .AddApplication<ApplicationServer>()
    .AddApplication<EFCoreInfrastructure>()
    .AddApplication<EFCoreSqliteInfrastructure>()
    .AddApplication<IdentityInfrastructure>()
    .AddApplication<EFCoreIdentityInfrastructure>()
    .AddApplication<EFCoreLocalStoreInfrastructure>()
    .AddApplication<PasskeysInfrastructure>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
