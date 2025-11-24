using Domain.Identity.ValueObjects;

namespace Domain.Identity.Interfaces;

public interface IUserSecretValidator
{
    bool Verify(PasswordCredential credential, ReadOnlySpan<char> secret);
}
