using Application.Server.Identity.Interfaces;
using Application.Server.Identity.Interfaces.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Application.Server.Identity.Services;

public sealed class AnonymousUserCleanupService(
    IUserRepository userRepository,
    ILogger<AnonymousUserCleanupService> logger) : IAnonymousUserCleanupService
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
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

        var deletedCount = await _userRepository.DeleteAbandonedAnonymousUsersAsync(cutoffDate, cancellationToken);

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
