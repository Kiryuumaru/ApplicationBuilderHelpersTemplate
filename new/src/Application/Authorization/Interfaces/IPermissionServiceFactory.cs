using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Authorization.Interfaces;

public interface IPermissionServiceFactory
{
    IPermissionService Create(Func<CancellationToken, Task<IJwtTokenService>> jwtTokenServiceFactory);
}
