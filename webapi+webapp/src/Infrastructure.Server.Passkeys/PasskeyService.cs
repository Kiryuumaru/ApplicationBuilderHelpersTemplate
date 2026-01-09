using Application.Server.Identity.Interfaces;
using Application.Server.Identity.Interfaces.Infrastructure;
using Application.Server.Identity.Models;
using Domain.Identity.Enums;
using Domain.Identity.Exceptions;
using Domain.Identity.Models;
using Domain.Shared.Exceptions;
using Fido2NetLib;
using Fido2NetLib.Objects;
using System.Text.Json;

namespace Infrastructure.Server.Passkeys;

/// <summary>
/// Passkey service implementation using Fido2.AspNet library.
/// </summary>
internal class PasskeyService : IPasskeyService
{
    private readonly IFido2 _fido2;
    private readonly IPasskeyRepository _passkeyRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserTokenService _userTokenService;
    private readonly JsonSerializerOptions _jsonOptions;

    public PasskeyService(
        IFido2 fido2,
        IPasskeyRepository passkeyRepository,
        IUserRepository userRepository,
        IUserTokenService userTokenService)
    {
        _fido2 = fido2 ?? throw new ArgumentNullException(nameof(fido2));
        _passkeyRepository = passkeyRepository ?? throw new ArgumentNullException(nameof(passkeyRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _userTokenService = userTokenService ?? throw new ArgumentNullException(nameof(userTokenService));
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public async Task<PasskeyCreationOptions> GetRegistrationOptionsAsync(
        Guid userId,
        string credentialName,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        // Get existing credentials to exclude
        var existingCredentials = await _passkeyRepository.GetCredentialsByUserIdAsync(userId, cancellationToken);
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

        // Store the challenge with credential name
        var challenge = PasskeyChallenge.Create(
            options.Challenge,
            userId,
            PasskeyChallengeType.Registration,
            options.ToJson(),
            credentialName);

        await _passkeyRepository.SaveChallengeAsync(challenge, cancellationToken);

        return new PasskeyCreationOptions(challenge.Id, options.ToJson());
    }

    public async Task<PasskeyRegistrationResult> VerifyRegistrationAsync(
        Guid challengeId,
        string attestationResponseJson,
        CancellationToken cancellationToken)
    {
        // Get the stored challenge
        var challenge = await _passkeyRepository.GetChallengeByIdAsync(challengeId, cancellationToken)
            ?? throw new PasskeyException("Challenge not found or expired", challengeId: challengeId);

        if (challenge.Type != PasskeyChallengeType.Registration)
            throw new PasskeyException("Invalid challenge type", challengeId: challengeId);

        if (challenge.IsExpired())
        {
            await _passkeyRepository.DeleteChallengeAsync(challengeId, cancellationToken);
            throw new PasskeyException("Challenge has expired", challengeId: challengeId);
        }

        if (!challenge.UserId.HasValue)
            throw new PasskeyException("Registration challenge must have a user ID", challengeId: challengeId);

        // Parse the original options
        var options = CredentialCreateOptions.FromJson(challenge.OptionsJson);

        // Parse the attestation response
        var attestationResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(
            attestationResponseJson, _jsonOptions)
            ?? throw new ValidationException("Invalid attestation response");

        // Verify the attestation
        var result = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = attestationResponse,
            OriginalOptions = options,
            IsCredentialIdUniqueToUserCallback = async (args, ct) =>
            {
                // Check if credential already exists
                var existing = await _passkeyRepository.GetCredentialByCredentialIdAsync(args.CredentialId, ct);
                return existing == null;
            }
        }, cancellationToken);

        if (result == null)
            throw new PasskeyException("Registration verification failed", challengeId: challengeId);

        // Create and store the credential using name from challenge entity
        var credential = PasskeyCredential.Create(
            challenge.UserId.Value,
            challenge.CredentialName ?? "My Passkey",
            result.Id,
            result.PublicKey,
            result.SignCount,
            result.AaGuid,
            result.Type.ToString(),
            result.User.Id,
            result.AttestationFormat);

        await _passkeyRepository.SaveCredentialAsync(credential, cancellationToken);

        // Delete the used challenge
        await _passkeyRepository.DeleteChallengeAsync(challengeId, cancellationToken);

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
            var user = await _userRepository.FindByUsernameAsync(username, cancellationToken);
            if (user != null)
            {
                userId = user.Id;
                var credentials = await _passkeyRepository.GetCredentialsByUserIdAsync(user.Id, cancellationToken);
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

        await _passkeyRepository.SaveChallengeAsync(challenge, cancellationToken);

        return new PasskeyRequestOptions(challenge.Id, options.ToJson());
    }

    public async Task<PasskeyLoginResult> VerifyLoginAsync(
        Guid challengeId,
        string assertionResponseJson,
        CancellationToken cancellationToken)
    {
        // Get the stored challenge
        var challenge = await _passkeyRepository.GetChallengeByIdAsync(challengeId, cancellationToken)
            ?? throw new PasskeyException("Challenge not found or expired", challengeId: challengeId);

        if (challenge.Type != PasskeyChallengeType.Authentication)
            throw new PasskeyException("Invalid challenge type", challengeId: challengeId);

        if (challenge.IsExpired())
        {
            await _passkeyRepository.DeleteChallengeAsync(challengeId, cancellationToken);
            throw new PasskeyException("Challenge has expired", challengeId: challengeId);
        }

        // Parse the original options
        var options = AssertionOptions.FromJson(challenge.OptionsJson);

        // Parse the assertion response
        var assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
            assertionResponseJson, _jsonOptions)
            ?? throw new ValidationException("Invalid assertion response");

        // Validate RawId is present
        if (assertionResponse.RawId == null || assertionResponse.RawId.Length == 0)
            throw new ValidationException("Assertion response missing credential ID");

        // Find the credential
        var credential = await _passkeyRepository.GetCredentialByCredentialIdAsync(assertionResponse.RawId, cancellationToken)
            ?? throw new EntityNotFoundException("Credential", Convert.ToBase64String(assertionResponse.RawId));

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
        await _passkeyRepository.UpdateCredentialAsync(credential, cancellationToken);

        // Delete the used challenge
        await _passkeyRepository.DeleteChallengeAsync(challengeId, cancellationToken);

        // Delegate to IUserTokenService for proper token generation
        var tokenResult = await _userTokenService.CreateSessionWithTokensAsync(
            credential.UserId,
            deviceInfo: null, // Passkey doesn't have device info context here
            cancellationToken);

        // Get the user for session info
        var user = await _userRepository.FindByIdAsync(credential.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("User", credential.UserId.ToString());

        // Create DTO with proper tokens from token service
        var sessionDto = new UserSessionDto
        {
            SessionId = tokenResult.SessionId,
            UserId = credential.UserId,
            Username = user.UserName,
            IsAnonymous = user.IsAnonymous,
            AccessToken = tokenResult.AccessToken,
            RefreshToken = tokenResult.RefreshToken,
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResult.ExpiresInSeconds),
            Roles = [] // Roles are encoded in the access token
        };

        return new PasskeyLoginResult(sessionDto, credential.Id);
    }

    public async Task<IReadOnlyCollection<PasskeyInfo>> ListPasskeysAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var credentials = await _passkeyRepository.GetCredentialsByUserIdAsync(userId, cancellationToken);
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
        var credential = await _passkeyRepository.GetCredentialByIdAsync(credentialId, cancellationToken)
            ?? throw new EntityNotFoundException("Passkey", credentialId.ToString());

        if (credential.UserId != userId)
            throw new UnauthorizedAccessException("You can only rename your own passkeys");

        credential.Rename(newName);
        await _passkeyRepository.UpdateCredentialAsync(credential, cancellationToken);
    }

    public async Task RevokePasskeyAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken)
    {
        var credential = await _passkeyRepository.GetCredentialByIdAsync(credentialId, cancellationToken)
            ?? throw new EntityNotFoundException("Passkey", credentialId.ToString());

        if (credential.UserId != userId)
            throw new UnauthorizedAccessException("You can only revoke your own passkeys");

        await _passkeyRepository.DeleteCredentialAsync(credentialId, cancellationToken);
    }
}
