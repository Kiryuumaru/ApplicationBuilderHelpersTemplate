using Infrastructure.EFCore;
using Infrastructure.EFCore.Sqlite;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Presentation.WebApp.Commands;
using Presentation.WebApp.Components;
using Presentation.WebApp.Components.Account;
using Presentation.WebApp.Data;

return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
    .AddApplication<Application.Application>()
    .AddApplication<InfrastructureEFCoreSqlite>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
