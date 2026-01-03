using Application.Identity.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Identity.Services;

internal sealed class AnonymousUserCleanupService(
    IUserStore userStore,
    ILogger<AnonymousUserCleanupService> logger) : IAnonymousUserCleanupService
{
    private readonly IUserStore _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
    private readonly ILogger<AnonymousUserCleanupService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<int> CleanupAbandonedAccountsAsync(int retentionDays, CancellationToken cancellationToken)
    {
        if (retentionDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays), "Retention days must be at least 1.");
        }

        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        _logger.LogInformation(
            "Starting cleanup of abandoned anonymous accounts older than {CutoffDate} ({RetentionDays} days)",
            cutoffDate,
            retentionDays);

        var deletedCount = await _userStore.DeleteAbandonedAnonymousUsersAsync(cutoffDate, cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Deleted {Count} abandoned anonymous user accounts", deletedCount);
        }
        else
        {
            _logger.LogDebug("No abandoned anonymous user accounts found to delete");
        }

        return deletedCount;
    }
}
