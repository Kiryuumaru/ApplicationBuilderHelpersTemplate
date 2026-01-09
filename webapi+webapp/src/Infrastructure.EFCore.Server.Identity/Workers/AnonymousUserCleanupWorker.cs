using Application.Server.Identity.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.EFCore.Server.Identity.Workers;

/// <summary>
/// Background service that periodically cleans up abandoned anonymous user accounts.
/// </summary>
internal sealed class AnonymousUserCleanupWorker(
    IServiceProvider serviceProvider,
    ILogger<AnonymousUserCleanupWorker> logger) : BackgroundService
{
    /// <summary>
    /// Number of days of inactivity before an anonymous account is considered abandoned.
    /// </summary>
    private const int RetentionDays = 30;

    /// <summary>
    /// How often to run the cleanup job.
    /// </summary>
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// Initial delay before the first cleanup run to allow the application to fully start.
    /// </summary>
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<AnonymousUserCleanupWorker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Anonymous user cleanup worker started. Will clean up accounts inactive for {RetentionDays} days every {Interval} hours",
            RetentionDays,
            CleanupInterval.TotalHours);

        // Wait before first run to let the application fully initialize
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during anonymous user cleanup");
            }

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Anonymous user cleanup worker stopped");
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var cleanupService = scope.ServiceProvider.GetRequiredService<IAnonymousUserCleanupService>();

        await cleanupService.CleanupAbandonedAccountsAsync(RetentionDays, cancellationToken);
    }
}
