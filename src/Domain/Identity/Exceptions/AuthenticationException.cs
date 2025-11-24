using Domain.Shared.Exceptions;

namespace Domain.Identity.Exceptions;

public sealed class AuthenticationException : DomainException
{
    public AuthenticationException(string message) : base(message)
    {
    }
}
