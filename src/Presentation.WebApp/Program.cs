using Infrastructure.Sqlite.Identity;
using Infrastructure.Sqlite.LocalStore;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Presentation.WebApp;
using Presentation.WebApp.Commands;
using Presentation.WebApp.Components;
using Presentation.WebApp.Components.Account;

return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
    .AddApplication<Application.Application>()
    .AddApplication<SqliteIdentityInfrastructure>()
    .AddApplication<SqliteLocalStoreInfrastructure>()
    .AddApplication<PresentationWebAppDependency>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
