namespace Application.Identity.Models;

/// <summary>
/// Result of passkey authentication (assertion verification).
/// </summary>
public record PasskeyLoginResult(
    UserSessionDto Session,
    Guid CredentialId
);
