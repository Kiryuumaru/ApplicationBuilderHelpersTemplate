using Domain.Identity.Models;

namespace Application.Identity.Models;

/// <summary>
/// Result of passkey authentication (assertion verification).
/// </summary>
public record PasskeyLoginResult(
    UserSession Session,
    Guid CredentialId
);
