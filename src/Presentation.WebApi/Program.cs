using Infrastructure.EFCore;
using Infrastructure.EFCore.Identity;
using Infrastructure.EFCore.LocalStore;
using Infrastructure.EFCore.Sqlite;
using Infrastructure.Identity;
using Infrastructure.Passkeys;
using Presentation.WebApi.Commands;

return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
    .AddApplication<Application.Application>()
    .AddApplication<EFCoreInfrastructure>()
    .AddApplication<EFCoreSqliteInfrastructure>()
    .AddApplication<IdentityInfrastructure>()
    .AddApplication<EFCoreIdentityInfrastructure>()
    .AddApplication<EFCoreLocalStoreInfrastructure>()
    .AddApplication<PasskeysInfrastructure>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
