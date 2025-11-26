using Application.Abstractions.Application;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Application.Logger.Extensions;
using Application.LocalStore.Interfaces;

namespace Presentation.Cli.Commands;

[Command(description: "Main subcommand.")]
public class MainCommand : Build.BaseCommand<HostApplicationBuilder>
{
    protected override ValueTask<HostApplicationBuilder> ApplicationBuilder(CancellationToken stoppingToken)
    {
        return new ValueTask<HostApplicationBuilder>(Host.CreateApplicationBuilder());
    }

    protected override async ValueTask Run(ApplicationHost<HostApplicationBuilder> applicationHost, CancellationTokenSource cancellationTokenSource)
    {
        await base.Run(applicationHost, cancellationTokenSource);

        var logger = applicationHost.Services.GetRequiredService<ILogger<MainCommand>>();
        var localStoreFactory = applicationHost.Services.GetRequiredService<ILocalStoreFactory>();

        using var _ = logger.BeginScopeMap(scopeMap: new Dictionary<string, object?>
        {
            { "AppName", ApplicationConstants.AppName },
            { "AppTitle", ApplicationConstants.AppTitle }
        });

        logger.LogTrace("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogDebug("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogInformation("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogWarning("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogError("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogCritical("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);

        logger.LogInformation("Boolean (true): {Value}", true);
        logger.LogInformation("Boolean (false): {Value}", false);
        logger.LogInformation("Integer: {Value}", 123);
        logger.LogInformation("Float: {Value}", 123.45f);
        logger.LogInformation("Double: {Value}", 3.14159);
        logger.LogInformation("Decimal: {Value}", 99.99m);
        logger.LogInformation("String: {Value}", "Hello, world!");
        logger.LogInformation("Null: {Value}", null!);
        logger.LogInformation("DateTime: {Value}", DateTime.Now);
        logger.LogInformation("Guid: {Value}", Guid.NewGuid());
        logger.LogInformation("Object: {@Value}", new { Name = "Test", Age = 42 });
        logger.LogInformation("Array: {@Value}", new[] { 1, 2, 3 });
        logger.LogInformation("Enum: {Value}", ConsoleColor.Red);

        // Open a store - transaction starts here at Open() and lasts for the entire using scope
        using var localStore = await localStoreFactory.OpenStore("common_group", cancellationTokenSource.Token);

        await localStore.Set("TestKey", "TestValue", cancellationTokenSource.Token);

        var value = await localStore.Get("TestKey", cancellationTokenSource.Token) ?? "<null>";

        logger.LogInformation("Retrieved value from local store: {Value}", value);
        
        // Explicitly commit the transaction before disposal (optional - will auto-commit on dispose)
        await localStore.CommitAsync(cancellationTokenSource.Token);

        // The transaction is automatically committed when the using block ends if not already committed

        cancellationTokenSource.Cancel();
    }
}
