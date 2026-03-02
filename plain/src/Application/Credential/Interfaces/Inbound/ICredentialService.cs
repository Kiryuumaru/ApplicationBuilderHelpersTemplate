namespace Application.Credential.Interfaces.Inbound;

public interface ICredentialService
{
    Task<Models.Credentials> GetCredentials(CancellationToken cancellationToken);
}
