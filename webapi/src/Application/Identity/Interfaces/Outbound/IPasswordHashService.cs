using Domain.Identity.Models;

namespace Application.Identity.Interfaces.Outbound;

/// <summary>
/// Service for hashing passwords.
/// Implemented by Infrastructure layer.
/// </summary>
public interface IPasswordHashService
{
    string Hash(User user, string password);
}
