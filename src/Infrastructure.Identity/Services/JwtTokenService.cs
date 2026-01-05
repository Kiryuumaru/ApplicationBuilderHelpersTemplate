using Application.Authorization.Models;
using Domain.Shared.Exceptions;
using Infrastructure.Identity.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Infrastructure.Identity.Services;

internal class JwtTokenService(Lazy<Func<CancellationToken, Task<JwtConfiguration>>> jwtConfigurationFactory) : IJwtTokenService
{
    private const string RbacVersionClaimType = "rbac_version";
    private const string ScopeClaimType = "scope";
    private const string CurrentRbacVersion = "2";

    public async Task<string> GenerateToken(
        string userId,
        string username,
        IEnumerable<string>? scopes = null,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var jwtConfiguration = await jwtConfigurationFactory.Value(cancellationToken);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfiguration.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, username),
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        if (additionalClaims is not null)
        {
            foreach (var claim in additionalClaims)
            {
                if (claim is null)
                {
                    continue;
                }

                if (IsReservedIdentityClaimType(claim.Type))
                {
                    continue;
                }

                if (string.Equals(claim.Type, ScopeClaimType, StringComparison.Ordinal))
                {
                    continue;
                }

                var duplicate = claims.Any(existing =>
                    string.Equals(existing.Type, claim.Type, StringComparison.Ordinal) &&
                    string.Equals(existing.Value, claim.Value, StringComparison.Ordinal) &&
                    string.Equals(existing.ValueType, claim.ValueType, StringComparison.Ordinal));

                if (!duplicate)
                {
                    claims.Add(CloneClaim(claim));
                }
            }
        }

        if (scopes is not null)
        {
            var seenScopes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var scope in scopes)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    continue;
                }

                var trimmed = scope.Trim();

                if (seenScopes.Add(trimmed))
                {
                    claims.Add(new Claim(ScopeClaimType, trimmed));
                }
            }
        }

        if (!claims.Any(claim => string.Equals(claim.Type, RbacVersionClaimType, StringComparison.Ordinal)))
        {
            claims.Add(new Claim(RbacVersionClaimType, CurrentRbacVersion));
        }

        var now = DateTime.UtcNow;
        DateTime expirationTime;

        if (expiration.HasValue)
        {
            var normalizedExpiration = NormalizeToUtc(expiration.Value.UtcDateTime);
            if (normalizedExpiration == default)
            {
                throw new SecurityTokenException("Expiration override must provide a valid timestamp.");
            }

            if (normalizedExpiration <= now)
            {
                throw new SecurityTokenException("Token expiration must be in the future.");
            }

            expirationTime = normalizedExpiration;
        }
        else
        {
            expirationTime = now.Add(jwtConfiguration.DefaultExpiration);
        }

        var token = new JwtSecurityToken(
            issuer: jwtConfiguration.Issuer,
            audience: jwtConfiguration.Audience,
            claims: claims,
            expires: expirationTime,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> GenerateApiKeyToken(
        string apiKeyName,
        IEnumerable<string>? scopes = null,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, apiKeyName),
            new("api_key", "true"),
            new(JwtRegisteredClaimNames.Sub, apiKeyName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        if (additionalClaims is not null)
        {
            foreach (var claim in additionalClaims)
            {
                if (claim is null)
                {
                    continue;
                }

                if (IsReservedIdentityClaimType(claim.Type))
                {
                    continue;
                }

                if (string.Equals(claim.Type, ScopeClaimType, StringComparison.Ordinal))
                {
                    continue;
                }

                claims.Add(CloneClaim(claim));
            }
        }

        return await GenerateToken(
            userId: apiKeyName,
            username: apiKeyName,
            scopes: scopes,
            additionalClaims: claims,
            expiration: expiration,
            cancellationToken: cancellationToken);
    }

    public async Task<TokenValidationParameters> GetTokenValidationParameters(CancellationToken cancellationToken = default)
    {
        var jwtConfiguration = await jwtConfigurationFactory.Value(cancellationToken);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfiguration.Secret));

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfiguration.Issuer,
            ValidAudience = jwtConfiguration.Audience,
            IssuerSigningKey = key,
            ClockSkew = jwtConfiguration.ClockSkew
        };
    }

    public async Task<ClaimsPrincipal?> ValidateToken(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var validationParameters = await GetTokenValidationParameters(cancellationToken);

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            if (validatedToken is JwtSecurityToken jwtToken)
            {
                var expirationUtc = jwtToken.ValidTo.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(jwtToken.ValidTo, DateTimeKind.Utc)
                    : jwtToken.ValidTo.ToUniversalTime();

                if (expirationUtc != default)
                {
                    var now = DateTime.UtcNow;
                    if (expirationUtc.Add(validationParameters.ClockSkew) < now)
                    {
                        throw new SecurityTokenExpiredException($"The token expired at {expirationUtc:O}.");
                    }
                }
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public Task<TokenInfo?> DecodeToken(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);

            var issuedAt = ExtractLifetime(jwtToken, JwtRegisteredClaimNames.Iat, jwtToken.IssuedAt);
            var expiresAt = ExtractLifetime(jwtToken, JwtRegisteredClaimNames.Exp, jwtToken.ValidTo);

            var claims = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);

            foreach (var claim in jwtToken.Claims)
            {
                var currentValue = ConvertToJsonNode(claim);

                if (claims.TryGetValue(claim.Type, out var existingValue))
                {
                    claims[claim.Type] = MergeClaimValues(existingValue, currentValue);
                }
                else
                {
                    claims[claim.Type] = currentValue;
                }
            }

            return Task.FromResult<TokenInfo?>(new TokenInfo
            {
                Subject = jwtToken.Subject,
                Issuer = jwtToken.Issuer,
                Audience = jwtToken.Audiences.FirstOrDefault(),
                IssuedAt = issuedAt,
                Expires = expiresAt,
                Claims = claims
            });
        }
        catch
        {
            return Task.FromResult<TokenInfo?>(null);
        }
    }

    public async Task<string> MutateToken(
        string token,
        IEnumerable<string>? scopesToAdd = null,
        IEnumerable<string>? scopesToRemove = null,
        IEnumerable<Claim>? claimsToAdd = null,
        IEnumerable<Claim>? claimsToRemove = null,
        IEnumerable<string>? claimTypesToRemove = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token must be provided.", nameof(token));
        }

        var principal = await ValidateToken(token, cancellationToken) ?? throw new SecurityTokenException("Token validation failed.");

        JwtSecurityToken jwtToken;
        try
        {
            jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
        }
        catch (Exception ex)
        {
            throw new SecurityTokenException("Token validation failed.", ex);
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ValidationException("Token does not contain a subject identifier claim.");
        }

        var username = principal.FindFirstValue(ClaimTypes.Name) ?? principal.Identity?.Name ?? userId;

        var mutableClaims = principal.Claims
            .Where(static claim => !IsReservedIdentityClaimType(claim.Type)
                && !string.Equals(claim.Type, ScopeClaimType, StringComparison.Ordinal))
            .Select(CloneClaim)
            .ToList();

        var (existingScopes, scopeSet) = ExtractScopes(principal);

        if (scopesToRemove is not null)
        {
            foreach (var scope in scopesToRemove)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    continue;
                }

                var trimmed = scope.Trim();

                if (!scopeSet.Remove(trimmed))
                {
                    continue;
                }

                existingScopes.RemoveAll(existing => string.Equals(existing, trimmed, StringComparison.Ordinal));
            }
        }

        if (scopesToAdd is not null)
        {
            foreach (var scope in scopesToAdd)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    continue;
                }

                var trimmed = scope.Trim();

                if (scopeSet.Add(trimmed))
                {
                    existingScopes.Add(trimmed);
                }
            }
        }

        if (claimTypesToRemove is not null)
        {
            foreach (var type in claimTypesToRemove)
            {
                if (string.IsNullOrWhiteSpace(type))
                {
                    continue;
                }

                var trimmed = type.Trim();
                if (IsReservedIdentityClaimType(trimmed) || string.Equals(trimmed, ScopeClaimType, StringComparison.Ordinal))
                {
                    throw new ValidationException($"Claim type '{trimmed}' cannot be removed.");
                }

                mutableClaims.RemoveAll(claim => string.Equals(claim.Type, trimmed, StringComparison.Ordinal));
            }
        }

        if (claimsToRemove is not null)
        {
            foreach (var claim in claimsToRemove)
            {
                if (claim is null || string.IsNullOrWhiteSpace(claim.Type))
                {
                    continue;
                }

                if (IsReservedIdentityClaimType(claim.Type))
                {
                    throw new ValidationException($"Claim type '{claim.Type}' cannot be removed.");
                }

                if (string.Equals(claim.Type, ScopeClaimType, StringComparison.Ordinal))
                {
                    throw new ValidationException("Scope claims must be removed via scopesToRemove.");
                }

                mutableClaims.RemoveAll(existing =>
                    string.Equals(existing.Type, claim.Type, StringComparison.Ordinal) &&
                    string.Equals(existing.Value, claim.Value, StringComparison.Ordinal));
            }
        }

        if (claimsToAdd is not null)
        {
            foreach (var claim in claimsToAdd)
            {
                if (claim is null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(claim.Type))
                {
                    throw new ArgumentException("Claims to add must define a type.", nameof(claimsToAdd));
                }

                if (IsReservedIdentityClaimType(claim.Type))
                {
                    throw new ValidationException($"Claim type '{claim.Type}' cannot be added.");
                }

                if (string.Equals(claim.Type, ScopeClaimType, StringComparison.Ordinal))
                {
                    throw new ValidationException("Scope claims must be added via scopesToAdd.");
                }

                var alreadyPresent = mutableClaims.Any(existing =>
                    string.Equals(existing.Type, claim.Type, StringComparison.Ordinal) &&
                    string.Equals(existing.Value, claim.Value, StringComparison.Ordinal) &&
                    string.Equals(existing.ValueType, claim.ValueType, StringComparison.Ordinal));

                if (!alreadyPresent)
                {
                    mutableClaims.Add(CloneClaim(claim));
                }
            }
        }

        var now = DateTime.UtcNow;
        DateTimeOffset? effectiveExpiration = null;

        if (expiration.HasValue)
        {
            var normalizedOverride = NormalizeToUtc(expiration.Value.UtcDateTime);
            if (normalizedOverride == default)
            {
                throw new SecurityTokenException("Expiration override must provide a valid timestamp.");
            }

            if (normalizedOverride <= now)
            {
                throw new SecurityTokenException("Token expiration must be in the future.");
            }

            effectiveExpiration = new DateTimeOffset(normalizedOverride, TimeSpan.Zero);
        }
        else
        {
            var normalizedExpiration = NormalizeExpiration(jwtToken.ValidTo);
            if (normalizedExpiration is not null)
            {
                if (normalizedExpiration <= now)
                {
                    throw new SecurityTokenException("Token has already expired.");
                }

                effectiveExpiration = new DateTimeOffset(normalizedExpiration.Value, TimeSpan.Zero);
            }
        }

        return await GenerateToken(
            userId: userId,
            username: username,
            scopes: existingScopes,
            additionalClaims: mutableClaims,
            expiration: effectiveExpiration,
            cancellationToken: cancellationToken);
    }

    private static (List<string> Items, HashSet<string> Set) ExtractScopes(ClaimsPrincipal principal)
    {
        var items = new List<string>();
        var set = new HashSet<string>(StringComparer.Ordinal);

        foreach (var claim in principal.Claims)
        {
            if (!string.Equals(claim.Type, ScopeClaimType, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(claim.Value))
            {
                continue;
            }

            var values = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (values.Length == 0)
            {
                continue;
            }

            foreach (var value in values)
            {
                var trimmed = value.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (set.Add(trimmed))
                {
                    items.Add(trimmed);
                }
            }
        }

        return (items, set);
    }

    private static Claim CloneClaim(Claim source)
    {
        var clone = new Claim(source.Type, source.Value, source.ValueType, source.Issuer, source.OriginalIssuer);
        foreach (var property in source.Properties)
        {
            clone.Properties[property.Key] = property.Value;
        }

        return clone;
    }

    private static bool IsReservedIdentityClaimType(string claimType)
    {
        return string.Equals(claimType, ClaimTypes.NameIdentifier, StringComparison.Ordinal)
            || string.Equals(claimType, ClaimTypes.Name, StringComparison.Ordinal)
            || string.Equals(claimType, JwtRegisteredClaimNames.Sub, StringComparison.Ordinal)
            || string.Equals(claimType, JwtRegisteredClaimNames.Jti, StringComparison.Ordinal)
            || string.Equals(claimType, JwtRegisteredClaimNames.Iat, StringComparison.Ordinal);
    }

    private static JsonNode? MergeClaimValues(JsonNode? existing, JsonNode? additional)
    {
        if (additional is null)
        {
            return existing;
        }

        if (existing is null)
        {
            return additional;
        }

        if (existing is JsonArray existingArray)
        {
            existingArray.Add(CloneNode(additional));
            return existingArray;
        }

        var array = new JsonArray
        {
            CloneNode(existing),
            CloneNode(additional)
        };

        return array;
    }

    private static JsonNode? ConvertToJsonNode(Claim claim)
    {
        var value = claim.Value;

        if (string.IsNullOrEmpty(value))
        {
            return JsonValue.Create(value);
        }

        if (IsNumericClaim(claim, out var numericNode))
        {
            return numericNode;
        }

        if (IsBooleanClaim(claim, out var booleanNode))
        {
            return booleanNode;
        }

        try
        {
            return JsonNode.Parse(value);
        }
        catch (JsonException)
        {
            return JsonValue.Create(value);
        }
    }

    private static JsonNode? CloneNode(JsonNode? node) => node?.DeepClone();

    private static bool IsNumericClaim(Claim claim, out JsonNode? node)
    {
        node = null;

        switch (claim.ValueType)
        {
            case ClaimValueTypes.Integer64:
            case ClaimValueTypes.Integer32:
            case ClaimValueTypes.Integer:
            case ClaimValueTypes.UInteger64:
            case ClaimValueTypes.UInteger32:
                if (long.TryParse(claim.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                {
                    node = JsonValue.Create(longValue);
                    return true;
                }
                break;
            case ClaimValueTypes.Double:
                if (double.TryParse(claim.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
                {
                    node = JsonValue.Create(doubleValue);
                    return true;
                }
                break;
        }

        return false;
    }

    private static bool IsBooleanClaim(Claim claim, out JsonNode? node)
    {
        node = null;

        if (string.Equals(claim.ValueType, ClaimValueTypes.Boolean, StringComparison.Ordinal) && bool.TryParse(claim.Value, out var booleanValue))
        {
            node = JsonValue.Create(booleanValue);
            return true;
        }

        return false;
    }

    private static DateTime ExtractLifetime(JwtSecurityToken token, string claimType, DateTime fallback)
    {
        var normalizedFallback = NormalizeToUtc(fallback);
        if (normalizedFallback != default)
        {
            return normalizedFallback;
        }

        if (token.Payload.TryGetValue(claimType, out var value) && TryConvertToUtcDateTime(value, out var parsed))
        {
            return parsed;
        }

        return default;
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        if (value == default)
        {
            return default;
        }

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value
        };
    }

    private static bool TryConvertToUtcDateTime(object value, out DateTime result)
    {
        switch (value)
        {
            case long longValue:
                result = DateTimeOffset.FromUnixTimeSeconds(longValue).UtcDateTime;
                return true;
            case int intValue:
                result = DateTimeOffset.FromUnixTimeSeconds(intValue).UtcDateTime;
                return true;
            case string stringValue when long.TryParse(stringValue, out var parsedLong):
                result = DateTimeOffset.FromUnixTimeSeconds(parsedLong).UtcDateTime;
                return true;
            case DateTimeOffset dto:
                result = dto.UtcDateTime;
                return true;
            case DateTime dateTimeValue:
            {
                var normalized = NormalizeToUtc(dateTimeValue);
                if (normalized != default)
                {
                    result = normalized;
                    return true;
                }

                result = default;
                return false;
            }
            default:
                result = default;
                return false;
        }
    }

    private static DateTime? NormalizeExpiration(DateTime value)
    {
        if (value == DateTime.MinValue)
        {
            return null;
        }

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime()
        };
    }
}
