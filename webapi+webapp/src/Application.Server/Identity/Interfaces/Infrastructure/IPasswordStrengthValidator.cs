using Domain.Identity.Models;

namespace Application.Server.Identity.Interfaces.Infrastructure;

public interface IPasswordStrengthValidator
{
    Task<IReadOnlyCollection<string>> ValidateAsync(User user, string password, CancellationToken cancellationToken);
}
