namespace Presentation.WebApi.Models.Responses;

/// <summary>
/// Response containing the list of passkeys for a user.
/// </summary>
/// <param name="Passkeys">The list of registered passkeys.</param>
public record PasskeyListResponse(
    IReadOnlyCollection<PasskeyInfoResponse> Passkeys
);
