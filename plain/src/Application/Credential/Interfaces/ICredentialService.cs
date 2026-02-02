namespace Application.Credential.Interfaces;

public interface ICredentialService
{
    Task<Models.Credentials> GetCredentials(CancellationToken cancellationToken);
}
