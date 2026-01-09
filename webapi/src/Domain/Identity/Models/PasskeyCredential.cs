using Domain.Shared.Models;

namespace Domain.Identity.Models;

/// <summary>
/// Represents a registered WebAuthn/FIDO2 passkey credential for a user.
/// </summary>
public sealed class PasskeyCredential : AuditableEntity
{
    /// <summary>
    /// The user who owns this passkey.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// User-friendly name for this passkey (e.g., "My iPhone", "Work Laptop").
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// The credential ID assigned by the authenticator (base64url encoded).
    /// This is used to identify which credential to use during authentication.
    /// </summary>
    public byte[] CredentialId { get; private set; }

    /// <summary>
    /// The public key in COSE format for verifying signatures.
    /// </summary>
    public byte[] PublicKey { get; private set; }

    /// <summary>
    /// The signature counter, incremented on each use.
    /// Used to detect cloned authenticators.
    /// </summary>
    public uint SignCount { get; private set; }

    /// <summary>
    /// The AAGUID of the authenticator that created this credential.
    /// </summary>
    public Guid AaGuid { get; private set; }

    /// <summary>
    /// The type of credential (typically "public-key").
    /// </summary>
    public string CredentialType { get; private set; }

    /// <summary>
    /// When this passkey was registered.
    /// </summary>
    public DateTimeOffset RegisteredAt { get; private set; }

    /// <summary>
    /// When this passkey was last used for authentication.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>
    /// User handle (user ID bytes) associated with this credential.
    /// </summary>
    public byte[] UserHandle { get; private set; }

    /// <summary>
    /// The attestation format used during registration (e.g., "none", "packed").
    /// </summary>
    public string AttestationFormat { get; private set; }

    private PasskeyCredential(
        Guid id,
        Guid userId,
        string name,
        byte[] credentialId,
        byte[] publicKey,
        uint signCount,
        Guid aaGuid,
        string credentialType,
        byte[] userHandle,
        string attestationFormat) : base(id)
    {
        UserId = userId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        CredentialId = credentialId ?? throw new ArgumentNullException(nameof(credentialId));
        PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
        SignCount = signCount;
        AaGuid = aaGuid;
        CredentialType = credentialType ?? "public-key";
        UserHandle = userHandle ?? throw new ArgumentNullException(nameof(userHandle));
        AttestationFormat = attestationFormat ?? "none";
        RegisteredAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Creates a new passkey credential after successful registration.
    /// </summary>
    public static PasskeyCredential Create(
        Guid userId,
        string name,
        byte[] credentialId,
        byte[] publicKey,
        uint signCount,
        Guid aaGuid,
        string credentialType,
        byte[] userHandle,
        string attestationFormat)
    {
        return new PasskeyCredential(
            Guid.NewGuid(),
            userId,
            string.IsNullOrWhiteSpace(name) ? "My Passkey" : name,
            credentialId,
            publicKey,
            signCount,
            aaGuid,
            credentialType,
            userHandle,
            attestationFormat);
    }

    /// <summary>
    /// Reconstructs a credential from stored data.
    /// </summary>
    public static PasskeyCredential Reconstruct(
        Guid id,
        Guid userId,
        string name,
        byte[] credentialId,
        byte[] publicKey,
        uint signCount,
        Guid aaGuid,
        string credentialType,
        byte[] userHandle,
        string attestationFormat,
        DateTimeOffset registeredAt,
        DateTimeOffset? lastUsedAt)
    {
        var credential = new PasskeyCredential
        {
            UserId = userId,
            Name = name,
            CredentialId = credentialId,
            PublicKey = publicKey,
            SignCount = signCount,
            AaGuid = aaGuid,
            CredentialType = credentialType,
            UserHandle = userHandle,
            AttestationFormat = attestationFormat,
            RegisteredAt = registeredAt,
            LastUsedAt = lastUsedAt
        };
        
        // Set Id using reflection since Entity.Id has a private setter
        typeof(Domain.Shared.Models.Entity)
            .GetProperty(nameof(Domain.Shared.Models.Entity.Id))!
            .SetValue(credential, id);
        
        return credential;
    }

    /// <summary>
    /// Updates the sign count after successful authentication.
    /// </summary>
    public void UpdateSignCount(uint newSignCount)
    {
        SignCount = newSignCount;
        LastUsedAt = DateTimeOffset.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// Renames this passkey.
    /// </summary>
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty", nameof(newName));

        Name = newName;
        MarkAsModified();
    }

    // For EF Core
    private PasskeyCredential() : base(Guid.NewGuid())
    {
        Name = string.Empty;
        CredentialId = Array.Empty<byte>();
        PublicKey = Array.Empty<byte>();
        CredentialType = "public-key";
        UserHandle = Array.Empty<byte>();
        AttestationFormat = "none";
    }
}
