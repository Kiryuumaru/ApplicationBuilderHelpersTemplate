using Infrastructure.Binance;
using Infrastructure.EFCore;
using Infrastructure.EFCore.Identity;
using Infrastructure.EFCore.LocalStore;
using Infrastructure.EFCore.Sqlite;
using Infrastructure.EFCore.Trading;
using Infrastructure.MarketData;
using Infrastructure.Passkeys;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Presentation.WebApp;
using Presentation.WebApp.Commands;
using Presentation.WebApp.Components;
using Presentation.WebApp.Components.Account;

return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
    .AddApplication<Application.Application>()
    .AddApplication<EFCoreInfrastructure>()
    .AddApplication<EFCoreSqliteInfrastructure>()
    .AddApplication<EFCoreIdentityInfrastructure>()
    .AddApplication<EFCoreLocalStoreInfrastructure>()
    .AddApplication<EFCoreTradingInfrastructure>()
    .AddApplication<BinanceInfrastructure>()
    .AddApplication<MarketDataInfrastructure>()
    .AddApplication<PasskeysInfrastructure>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
