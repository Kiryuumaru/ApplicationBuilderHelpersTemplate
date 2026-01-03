namespace Presentation.WebApi.Models.Responses;

/// <summary>
/// Response containing passkey registration options.
/// </summary>
/// <param name="ChallengeId">The challenge ID to use when completing registration.</param>
/// <param name="OptionsJson">The WebAuthn options JSON to pass to navigator.credentials.create().</param>
public record PasskeyRegistrationOptionsResponse(
    Guid ChallengeId,
    string OptionsJson
);

/// <summary>
/// Response after successfully registering a passkey.
/// </summary>
/// <param name="CredentialId">The unique identifier for this passkey.</param>
/// <param name="Name">The name assigned to this passkey.</param>
public record PasskeyRegistrationResponse(
    Guid CredentialId,
    string Name
);

/// <summary>
/// Response containing passkey login options.
/// </summary>
/// <param name="ChallengeId">The challenge ID to use when completing login.</param>
/// <param name="OptionsJson">The WebAuthn options JSON to pass to navigator.credentials.get().</param>
public record PasskeyLoginOptionsResponse(
    Guid ChallengeId,
    string OptionsJson
);

/// <summary>
/// Information about a registered passkey.
/// </summary>
/// <param name="Id">The unique identifier for this passkey.</param>
/// <param name="Name">The user-friendly name of this passkey.</param>
/// <param name="RegisteredAt">When this passkey was registered.</param>
/// <param name="LastUsedAt">When this passkey was last used for authentication.</param>
public record PasskeyInfoResponse(
    Guid Id,
    string Name,
    DateTimeOffset RegisteredAt,
    DateTimeOffset? LastUsedAt
);

/// <summary>
/// Response containing the list of passkeys for a user.
/// </summary>
/// <param name="Passkeys">The list of registered passkeys.</param>
public record PasskeyListResponse(
    IReadOnlyCollection<PasskeyInfoResponse> Passkeys
);
