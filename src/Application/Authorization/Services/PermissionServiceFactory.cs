using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Authorization.Interfaces;

namespace Application.Authorization.Services;

internal sealed class PermissionServiceFactory : IPermissionServiceFactory
{
    public IPermissionService Create(Func<CancellationToken, Task<IJwtTokenService>> jwtTokenServiceFactory)
    {
        ArgumentNullException.ThrowIfNull(jwtTokenServiceFactory);

        return new PermissionService(jwtTokenServiceFactory);
    }
}
