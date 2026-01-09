using Application.Server.Identity.Interfaces;
using Application.Server.Identity.Interfaces.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Application.Server.Identity.Services;

/// <summary>
/// Service for cleaning up expired and revoked API keys.
/// </summary>
public sealed class ApiKeyCleanupService(
    IApiKeyRepository apiKeyRepository,
    ILogger<ApiKeyCleanupService> logger) : IApiKeyCleanupService
{
    private readonly IApiKeyRepository _apiKeyRepository = apiKeyRepository ?? throw new ArgumentNullException(nameof(apiKeyRepository));
    private readonly ILogger<ApiKeyCleanupService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<int> CleanupExpiredAndRevokedKeysAsync(
        int expiredRetentionDays,
        int revokedRetentionDays,
        CancellationToken cancellationToken)
    {
        if (expiredRetentionDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expiredRetentionDays), "Expired retention days must be non-negative.");
        }

        if (revokedRetentionDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revokedRetentionDays), "Revoked retention days must be at least 1.");
        }

        var expiredBefore = DateTimeOffset.UtcNow.AddDays(-expiredRetentionDays);
        var revokedBefore = DateTimeOffset.UtcNow.AddDays(-revokedRetentionDays);

        _logger.LogInformation(
            "Starting API key cleanup. Deleting keys expired before {ExpiredBefore} and revoked before {RevokedBefore}",
            expiredBefore,
            revokedBefore);

        var deletedCount = await _apiKeyRepository.DeleteExpiredOrRevokedAsync(
            expiredBefore,
            revokedBefore,
            cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Deleted {Count} expired or revoked API keys", deletedCount);
        }
        else
        {
            _logger.LogDebug("No expired or revoked API keys found to delete");
        }

        return deletedCount;
    }
}
