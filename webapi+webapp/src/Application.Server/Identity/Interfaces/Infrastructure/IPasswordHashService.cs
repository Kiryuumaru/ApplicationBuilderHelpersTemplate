using Domain.Identity.Models;

namespace Application.Server.Identity.Interfaces.Infrastructure;

public interface IPasswordHashService
{
    string Hash(User user, string password);
}
