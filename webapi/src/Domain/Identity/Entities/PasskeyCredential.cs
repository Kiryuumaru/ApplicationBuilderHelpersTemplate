using Domain.Shared.Models;

namespace Domain.Identity.Entities;

public class PasskeyCredential : AuditableEntity
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; }
    public byte[] CredentialId { get; private set; }
    public byte[] PublicKey { get; private set; }
    public uint SignCount { get; private set; }
    public Guid AaGuid { get; private set; }
    public string CredentialType { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public byte[] UserHandle { get; private set; }
    public string AttestationFormat { get; private set; }

    protected PasskeyCredential(
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
        Name = name;
        CredentialId = credentialId;
        PublicKey = publicKey;
        SignCount = signCount;
        AaGuid = aaGuid;
        CredentialType = credentialType;
        UserHandle = userHandle;
        AttestationFormat = attestationFormat;
        RegisteredAt = DateTimeOffset.UtcNow;
    }

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
        ArgumentNullException.ThrowIfNull(credentialId);
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(userHandle);

        return new PasskeyCredential(
            Guid.NewGuid(),
            userId,
            string.IsNullOrWhiteSpace(name) ? "My Passkey" : name,
            credentialId,
            publicKey,
            signCount,
            aaGuid,
            credentialType ?? "public-key",
            userHandle,
            attestationFormat ?? "none");
    }

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
        typeof(global::Domain.Shared.Models.Entity)
            .GetProperty(nameof(global::Domain.Shared.Models.Entity.Id))!
            .SetValue(credential, id);
        
        return credential;
    }

    public void UpdateSignCount(uint newSignCount)
    {
        SignCount = newSignCount;
        LastUsedAt = DateTimeOffset.UtcNow;
        MarkAsModified();
    }

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
