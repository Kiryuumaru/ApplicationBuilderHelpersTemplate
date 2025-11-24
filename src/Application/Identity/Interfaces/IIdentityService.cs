using Application.Identity.Models;
using Domain.Identity.Models;

namespace Application.Identity.Interfaces;

public interface IIdentityService
{
    Task<User> RegisterUserAsync(UserRegistrationRequest request, CancellationToken cancellationToken);

    Task<User> RegisterExternalAsync(ExternalUserRegistrationRequest request, CancellationToken cancellationToken);

    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<User>> ListAsync(CancellationToken cancellationToken);

    Task<UserSession> AuthenticateAsync(string username, string password, CancellationToken cancellationToken);

    Task AssignRoleAsync(Guid userId, RoleAssignmentRequest assignment, CancellationToken cancellationToken);
}
