using Domain.Identity.Entities;
using Domain.Identity.Exceptions;
using Domain.Identity.Interfaces;
using Domain.Identity.Models;

namespace Domain.Identity.Services;

public sealed class UserAuthenticationService(IPasswordVerifier passwordVerifier)
{
    public void Authenticate(User user, string secret, DateTimeOffset? issuedAt = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        var timestamp = issuedAt ?? DateTimeOffset.UtcNow;
        if (!user.CanAuthenticate(timestamp))
        {
            throw new AuthenticationException("User is not allowed to authenticate in the current state.");
        }

        if (user.PasswordHash is null)
        {
            throw new AuthenticationException("User does not have a password set.");
        }

        if (!passwordVerifier.Verify(user.PasswordHash, secret))
        {
            user.RecordFailedLogin(timestamp);
            throw new AuthenticationException("Invalid credentials supplied.");
        }

        user.RecordSuccessfulLogin(timestamp);
    }
}
