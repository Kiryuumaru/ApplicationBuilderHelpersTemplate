namespace Application.Credential.Interfaces.Inbound;

public interface ICredentialService
{
    Task<Models.Credentials> GetCredentials(string envTag, CancellationToken cancellationToken);

    Task<Models.Credentials> GetCredentials(CancellationToken cancellationToken);
}
