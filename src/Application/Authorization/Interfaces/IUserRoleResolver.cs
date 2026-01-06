using Domain.Identity.Models;

namespace Application.Authorization.Interfaces;

public interface IUserRoleResolver
{
    Task<IReadOnlyCollection<UserRoleResolution>> ResolveRolesAsync(User user, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> ResolveRoleCodesAsync(User user, CancellationToken cancellationToken);
    
    /// <summary>
    /// Resolves roles and formats them with inline parameters (e.g., "USER;roleUserId=abc123").
    /// </summary>
    Task<IReadOnlyCollection<string>> ResolveFormattedRoleClaimsAsync(User user, CancellationToken cancellationToken);
}
