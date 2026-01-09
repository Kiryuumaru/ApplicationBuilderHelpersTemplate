using Domain.Identity.Models;

namespace Application.Identity.Interfaces.Infrastructure;

public interface IPasswordResetTokenService
{
    Task<string> GenerateResetTokenAsync(User user, CancellationToken cancellationToken);

    Task<bool> ResetPasswordWithTokenAsync(User user, string token, string newPassword, CancellationToken cancellationToken);
}
