using Domain.Identity.Models;

namespace Application.Identity.Interfaces.Infrastructure;

public interface IPasswordHashService
{
    string Hash(User user, string password);
}
