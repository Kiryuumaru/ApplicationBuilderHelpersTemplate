namespace Application.Credential.Interfaces.Inbound;

/// <summary>
/// Service for retrieving application credentials based on environment.
/// </summary>
public interface ICredentialService
{
    Task<Models.Credentials> GetCredentials(string envTag, CancellationToken cancellationToken);

    Task<Models.Credentials> GetCredentials(CancellationToken cancellationToken);
}
