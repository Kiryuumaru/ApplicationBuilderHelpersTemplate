using Application.Authorization.Interfaces;
using Application.Authorization.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Application.Authorization.Services;

internal class JwtTokenServiceFactory : IJwtTokenServiceFactory
{
    public IJwtTokenService Create(Func<CancellationToken, Task<JwtConfiguration>> jwtConfigurationFactory)
    {
        return new JwtTokenService(new Lazy<Func<CancellationToken, Task<JwtConfiguration>>>(async ct => await jwtConfigurationFactory(ct)));
    }
}
