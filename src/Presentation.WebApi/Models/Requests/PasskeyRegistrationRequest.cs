using System.ComponentModel.DataAnnotations;

namespace Presentation.WebApi.Models.Requests;

/// <summary>
/// Request to get registration options for creating a new passkey.
/// </summary>
/// <param name="CredentialName">User-friendly name for this passkey (e.g., "My iPhone", "Work Laptop").</param>
public record PasskeyRegistrationOptionsRequest(
    [Required]
    [MaxLength(256)]
    string CredentialName
);

/// <summary>
/// Request to complete passkey registration with the attestation response.
/// </summary>
/// <param name="ChallengeId">The challenge ID returned from the registration options endpoint.</param>
/// <param name="AttestationResponseJson">The JSON-serialized attestation response from the authenticator.</param>
public record PasskeyRegistrationRequest(
    [Required]
    Guid ChallengeId,
    [Required]
    string AttestationResponseJson
);
