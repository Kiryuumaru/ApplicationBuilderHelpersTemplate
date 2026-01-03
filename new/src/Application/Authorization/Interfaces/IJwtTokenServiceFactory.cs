using Application.Authorization.Models;
using System.Security.Claims;

namespace Application.Authorization.Interfaces;

/// <summary>
/// Interface for JWT token service factory that creates JWT token service instances.
/// </summary>
public interface IJwtTokenServiceFactory
{
    /// <summary>
    /// Creates a JWT token service instance with the specified configuration factory.
    /// </summary>
    /// <param name="jwtConfigurationFactory">A factory function that provides JWT configuration asynchronously.</param>
    /// <returns>An instance of IJwtTokenService.</returns>
    IJwtTokenService Create(Func<CancellationToken, Task<JwtConfiguration>> jwtConfigurationFactory);
}
