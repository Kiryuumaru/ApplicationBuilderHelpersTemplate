using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Authorization.Interfaces;

public interface IAuthorizedHttpClientFactory : IDisposable
{
    Task<HttpClient> CreateAuthorized(string clientName, IEnumerable<string> permissionIdentifiers, TimeSpan expiration, CancellationToken cancellationToken = default);
}
