using Domain.Identity.ValueObjects;

namespace Application.Identity.Interfaces;

public interface IPasswordCredentialFactory
{
    PasswordCredential Create(string secret, int? iterationOverride = null);
}
