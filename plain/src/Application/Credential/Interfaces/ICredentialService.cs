using Application.AppEnvironment.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Credential.Interfaces;

public interface ICredentialService
{
    Task<Models.Credentials> GetCredentials(CancellationToken cancellationToken);
}
