using Application.Identity.Interfaces;
using Application.Identity.Models;
using Domain.Identity.Enums;
using Domain.Identity.Models;
using Fido2NetLib;
using Fido2NetLib.Objects;
using System.Text.Json;

namespace Infrastructure.Passkeys;

/// <summary>
/// Passkey service implementation using Fido2.AspNet library.
/// </summary>
public class PasskeyService : IPasskeyService
{
    private readonly IFido2 _fido2;
    private readonly IPasskeyChallengeStore _challengeStore;
    private readonly IPasskeyCredentialStore _credentialStore;
    private readonly IUserStore _userStore;
    private readonly JsonSerializerOptions _jsonOptions;

    // Temporary storage for credential names during registration flow
    private static readonly Dictionary<Guid, string> _pendingCredentialNames = new();

    public PasskeyService(
        IFido2 fido2,
        IPasskeyChallengeStore challengeStore,
        IPasskeyCredentialStore credentialStore,
        IUserStore userStore)
    {
        _fido2 = fido2 ?? throw new ArgumentNullException(nameof(fido2));
        _challengeStore = challengeStore ?? throw new ArgumentNullException(nameof(challengeStore));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public async Task<PasskeyCreationOptions> GetRegistrationOptionsAsync(
        Guid userId,
        string credentialName,
        CancellationToken cancellationToken)
    {
        var user = await _userStore.FindByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException($"User {userId} not found");

        // Get existing credentials to exclude
        var existingCredentials = await _credentialStore.GetByUserIdAsync(userId, cancellationToken);
        var excludeCredentials = existingCredentials
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToList();

        // Create Fido2 user
        var fido2User = new Fido2User
        {
            Id = userId.ToByteArray(),
            Name = user.UserName ?? user.Email ?? userId.ToString(),
            DisplayName = user.UserName ?? user.Email ?? "User"
        };

        // Generate registration options
        var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fido2User,
            ExcludeCredentials = excludeCredentials,
            AuthenticatorSelection = AuthenticatorSelection.Default,
            AttestationPreference = AttestationConveyancePreference.None
        });

        // Store the challenge
        var challenge = PasskeyChallenge.Create(
            options.Challenge,
            userId,
            PasskeyChallengeType.Registration,
            options.ToJson());

        await _challengeStore.SaveAsync(challenge, cancellationToken);

        // Store the credential name for later
        _pendingCredentialNames[challenge.Id] = credentialName;

        return new PasskeyCreationOptions(challenge.Id, options.ToJson());
    }

    public async Task<PasskeyRegistrationResult> VerifyRegistrationAsync(
        Guid challengeId,
        string attestationResponseJson,
        CancellationToken cancellationToken)
    {
        // Get the stored challenge
        var challenge = await _challengeStore.GetByIdAsync(challengeId, cancellationToken)
            ?? throw new InvalidOperationException("Challenge not found or expired");

        if (challenge.Type != PasskeyChallengeType.Registration)
            throw new InvalidOperationException("Invalid challenge type");

        if (challenge.IsExpired())
        {
            await _challengeStore.DeleteAsync(challengeId, cancellationToken);
            throw new InvalidOperationException("Challenge has expired");
        }

        if (!challenge.UserId.HasValue)
            throw new InvalidOperationException("Registration challenge must have a user ID");

        // Parse the original options
        var options = CredentialCreateOptions.FromJson(challenge.OptionsJson);

        // Parse the attestation response
        var attestationResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(
            attestationResponseJson, _jsonOptions)
            ?? throw new InvalidOperationException("Invalid attestation response");

        // Verify the attestation
        var result = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = attestationResponse,
            OriginalOptions = options,
            IsCredentialIdUniqueToUserCallback = async (args, ct) =>
            {
                // Check if credential already exists
                var existing = await _credentialStore.GetByCredentialIdAsync(args.CredentialId, ct);
                return existing == null;
            }
        }, cancellationToken);

        if (result == null)
            throw new InvalidOperationException("Registration verification failed");

        // Get the credential name
        _pendingCredentialNames.TryGetValue(challengeId, out var credentialName);
        _pendingCredentialNames.Remove(challengeId);

        // Create and store the credential
        var credential = PasskeyCredential.Create(
            challenge.UserId.Value,
            credentialName ?? "My Passkey",
            result.Id,
            result.PublicKey,
            result.SignCount,
            result.AaGuid,
            result.Type.ToString(),
            result.User.Id,
            result.AttestationFormat);

        await _credentialStore.SaveAsync(credential, cancellationToken);

        // Delete the used challenge
        await _challengeStore.DeleteAsync(challengeId, cancellationToken);

        return new PasskeyRegistrationResult(credential.Id, credential.Name);
    }

    public async Task<PasskeyRequestOptions> GetLoginOptionsAsync(
        string? username,
        CancellationToken cancellationToken)
    {
        List<PublicKeyCredentialDescriptor> allowedCredentials = [];
        Guid? userId = null;

        if (!string.IsNullOrEmpty(username))
        {
            var user = await _userStore.FindByUsernameAsync(username, cancellationToken);
            if (user != null)
            {
                userId = user.Id;
                var credentials = await _credentialStore.GetByUserIdAsync(user.Id, cancellationToken);
                allowedCredentials = credentials
                    .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                    .ToList();
            }
        }

        // Generate assertion options
        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowedCredentials,
            UserVerification = UserVerificationRequirement.Preferred
        });

        // Store the challenge
        var challenge = PasskeyChallenge.Create(
            options.Challenge,
            userId,
            PasskeyChallengeType.Authentication,
            options.ToJson());

        await _challengeStore.SaveAsync(challenge, cancellationToken);

        return new PasskeyRequestOptions(challenge.Id, options.ToJson());
    }

    public async Task<PasskeyLoginResult> VerifyLoginAsync(
        Guid challengeId,
        string assertionResponseJson,
        CancellationToken cancellationToken)
    {
        // Get the stored challenge
        var challenge = await _challengeStore.GetByIdAsync(challengeId, cancellationToken)
            ?? throw new InvalidOperationException("Challenge not found or expired");

        if (challenge.Type != PasskeyChallengeType.Authentication)
            throw new InvalidOperationException("Invalid challenge type");

        if (challenge.IsExpired())
        {
            await _challengeStore.DeleteAsync(challengeId, cancellationToken);
            throw new InvalidOperationException("Challenge has expired");
        }

        // Parse the original options
        var options = AssertionOptions.FromJson(challenge.OptionsJson);

        // Parse the assertion response
        var assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
            assertionResponseJson, _jsonOptions)
            ?? throw new InvalidOperationException("Invalid assertion response");

        // Find the credential
        var credential = await _credentialStore.GetByCredentialIdAsync(assertionResponse.RawId, cancellationToken)
            ?? throw new InvalidOperationException("Credential not found");

        // Verify the assertion
        var result = await _fido2.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse = assertionResponse,
            OriginalOptions = options,
            StoredPublicKey = credential.PublicKey,
            StoredSignatureCounter = credential.SignCount,
            IsUserHandleOwnerOfCredentialIdCallback = async (args, ct) =>
            {
                // Verify the user handle matches
                if (args.UserHandle.Length > 0)
                {
                    var userIdFromHandle = new Guid(args.UserHandle);
                    return userIdFromHandle == credential.UserId;
                }
                return true;
            }
        }, cancellationToken);

        // If we get here, the assertion was valid (otherwise an exception would be thrown)
        
        // Update the sign count
        credential.UpdateSignCount(result.SignCount);
        await _credentialStore.UpdateAsync(credential, cancellationToken);

        // Delete the used challenge
        await _challengeStore.DeleteAsync(challengeId, cancellationToken);

        // Get the user
        var user = await _userStore.FindByIdAsync(credential.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"User {credential.UserId} not found");

        // Create a session using the user's method
        var session = user.CreateSession(TimeSpan.FromHours(24)); // Default 24 hour session

        return new PasskeyLoginResult(session, credential.Id);
    }

    public async Task<IReadOnlyCollection<PasskeyInfo>> ListPasskeysAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var credentials = await _credentialStore.GetByUserIdAsync(userId, cancellationToken);
        return credentials
            .Select(c => new PasskeyInfo(c.Id, c.Name, c.RegisteredAt, c.LastUsedAt))
            .OrderByDescending(c => c.RegisteredAt)
            .ToList();
    }

    public async Task RenamePasskeyAsync(
        Guid userId,
        Guid credentialId,
        string newName,
        CancellationToken cancellationToken)
    {
        var credential = await _credentialStore.GetByIdAsync(credentialId, cancellationToken)
            ?? throw new InvalidOperationException("Passkey not found");

        if (credential.UserId != userId)
            throw new UnauthorizedAccessException("You can only rename your own passkeys");

        credential.Rename(newName);
        await _credentialStore.UpdateAsync(credential, cancellationToken);
    }

    public async Task RevokePasskeyAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken)
    {
        var credential = await _credentialStore.GetByIdAsync(credentialId, cancellationToken)
            ?? throw new InvalidOperationException("Passkey not found");

        if (credential.UserId != userId)
            throw new UnauthorizedAccessException("You can only revoke your own passkeys");

        await _credentialStore.DeleteAsync(credentialId, cancellationToken);
    }
}
