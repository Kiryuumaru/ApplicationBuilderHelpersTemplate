namespace Domain.Identity.Enums;

/// <summary>
/// The type of passkey challenge being requested.
/// </summary>
public enum PasskeyChallengeType
{
    /// <summary>
    /// Challenge for registering a new passkey credential.
    /// </summary>
    Registration = 0,

    /// <summary>
    /// Challenge for authenticating with an existing passkey credential.
    /// </summary>
    Authentication = 1
}
