using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Domain.Authorization.Constants;
using Domain.Shared.Exceptions;
using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Infrastructure;
using Application.Authorization.Models;
using Domain.Authorization.Enums;
using Domain.Authorization.Models;
using Domain.Authorization.Services;
using Domain.Authorization.ValueObjects;

namespace Application.Authorization.Services;

internal sealed class PermissionService(
    ITokenService tokenService,
    IRoleRepository roleRepository) : IPermissionService
{
    private const string ScopeClaimType = "scope";
    private const string RbacVersionClaimType = "rbac_version";
    private const string CurrentRbacVersion = "2";

    private static readonly ReadOnlyCollection<Permission> PermissionTree;
    private static readonly ReadOnlyDictionary<string, Permission> PermissionLookup;
    private static readonly ReadOnlyCollection<string> PermissionIdentifiers;
    private static readonly ReadOnlyDictionary<string, HashSet<string>> ReachableParameterLookup;
    private static readonly IReadOnlyDictionary<string, string> EmptyParameterDictionary =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0, StringComparer.Ordinal));
    private static readonly HashSet<string> EmptyParameterNameSet = new(StringComparer.Ordinal);

    private readonly ITokenService _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
    private readonly IRoleRepository _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));

    static PermissionService()
    {
        PermissionTree = new ReadOnlyCollection<Permission>([.. Permissions.PermissionTreeRoots]);

        var allPermissions = Permissions.GetAll();
        var lookup = new Dictionary<string, Permission>(allPermissions.Count, StringComparer.Ordinal);
        foreach (var permission in allPermissions)
        {
            lookup[permission.Path] = permission;
        }

        PermissionLookup = lookup.AsReadOnly();

        var assignableIdentifiers = lookup
            .Where(static kvp => kvp.Value.AccessCategory != PermissionAccessCategory.Unspecified)
            .Select(static kvp => kvp.Key)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        PermissionIdentifiers = Array.AsReadOnly(assignableIdentifiers);

        ReachableParameterLookup = BuildReachableParameterLookup(PermissionTree);
    }

    public async Task<string> GenerateTokenWithPermissionsAsync(
        string userId,
        string? username,
        IEnumerable<string> permissionIdentifiers,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var normalizedPermissions = NormalizeAndValidate(permissionIdentifiers, allowEmpty: true);
        var additionalClaimSet = additionalClaims?.ToArray();

        return await _tokenService.GenerateTokenWithScopesAsync(
            userId: userId,
            username: username ?? string.Empty,
            scopes: normalizedPermissions,
            additionalClaims: additionalClaimSet,
            expiration: expiration,
            cancellationToken: cancellationToken);
    }

    public async Task<string> GenerateApiKeyTokenWithPermissionsAsync(
        string apiKeyName,
        IEnumerable<string> permissionIdentifiers,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKeyName);

        var normalizedPermissions = NormalizeAndValidate(permissionIdentifiers, allowEmpty: true);
        var additionalClaimSet = additionalClaims?.ToArray();

        return await _tokenService.GenerateApiKeyTokenAsync(
            apiKeyName: apiKeyName,
            scopes: normalizedPermissions,
            additionalClaims: additionalClaimSet,
            expiration: expiration,
            cancellationToken: cancellationToken);
    }

    public async Task<string> GenerateTokenWithScopeAsync(
        string userId,
        string? username,
        IEnumerable<ScopeDirective> scopeDirectives,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        // Convert scope directives to their string representation
        var scopes = scopeDirectives?
            .Where(static d => d != null)
            .Select(static d => d.ToString())
            .ToArray() ?? Array.Empty<string>();

        var additionalClaimSet = additionalClaims?.ToArray();

        return await _tokenService.GenerateTokenWithScopesAsync(
            userId: userId,
            username: username ?? string.Empty,
            scopes: scopes,
            additionalClaims: additionalClaimSet,
            expiration: expiration,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyCollection<Permission>> GetPermissionTreeAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<Permission>>(PermissionTree);

    public Task<IReadOnlyCollection<string>> GetAllPermissionIdentifiersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<string>>(PermissionIdentifiers);

    public Task<bool> ValidatePermissionsAsync(IEnumerable<string> permissionIdentifiers, CancellationToken cancellationToken = default)
    {
        if (permissionIdentifiers is null)
        {
            return Task.FromResult(false);
        }

        foreach (var identifier in permissionIdentifiers)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return Task.FromResult(false);
            }

            if (!Permission.TryParseIdentifier(identifier, out var parsed))
            {
                return Task.FromResult(false);
            }

            if (!PermissionLookup.TryGetValue(parsed.Canonical, out var permission))
            {
                return Task.FromResult(false);
            }

            if (permission.AccessCategory == PermissionAccessCategory.Unspecified)
            {
                return Task.FromResult(false);
            }

            if (!AreParametersValid(permission, parsed.Parameters, out _))
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    public Task<IReadOnlyCollection<Permission>> ResolvePermissionsAsync(
        IEnumerable<string> permissionIdentifiers,
        CancellationToken cancellationToken = default)
    {
        var normalizedPermissions = NormalizeAndValidate(permissionIdentifiers, allowEmpty: true);
        if (normalizedPermissions.Length == 0)
        {
            return Task.FromResult<IReadOnlyCollection<Permission>>([]);
        }

        var resolved = new List<Permission>(normalizedPermissions.Length);
        foreach (var identifier in normalizedPermissions)
        {
            if (!Permission.TryParseIdentifier(identifier, out var parsed))
            {
                continue;
            }

            if (PermissionLookup.TryGetValue(parsed.Canonical, out var permission))
            {
                resolved.Add(permission);
            }
        }

        return Task.FromResult<IReadOnlyCollection<Permission>>(resolved.Count == 0
            ? Array.Empty<Permission>()
            : [.. resolved]);
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permissionIdentifier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (string.IsNullOrWhiteSpace(permissionIdentifier))
        {
            return false;
        }

        var trimmed = permissionIdentifier.Trim();
        if (!Permission.TryParseIdentifier(trimmed, out var parsed))
        {
            return false;
        }

        if (!PermissionLookup.TryGetValue(parsed.Canonical, out var permission))
        {
            return false;
        }

        if (!AreParametersValid(permission, parsed.Parameters, out _))
        {
            return false;
        }

        // Check RBAC version and use appropriate evaluation strategy
        var rbacVersion = GetRbacVersion(principal);

        if (IsLegacyRbacVersion(rbacVersion))
        {
            // Legacy tokens (null or "1") get full admin access for backward compatibility
            return true;
        }

        // Version 2+: Resolve roles at runtime and evaluate
        var scope = await ResolveScopeDirectivesAsync(principal, cancellationToken).ConfigureAwait(false);
        return ScopeEvaluator.HasPermission(scope, parsed.Canonical, parsed.Parameters);
    }

    public async Task<bool> HasAnyPermissionAsync(ClaimsPrincipal principal, IEnumerable<string> permissionIdentifiers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (permissionIdentifiers is null)
        {
            return false;
        }

        var rbacVersion = GetRbacVersion(principal);

        if (IsLegacyRbacVersion(rbacVersion))
        {
            // Legacy tokens get full admin access
            return permissionIdentifiers.Any(id => !string.IsNullOrWhiteSpace(id));
        }

        var scope = await ResolveScopeDirectivesAsync(principal, cancellationToken).ConfigureAwait(false);

        foreach (var identifier in permissionIdentifiers)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                continue;
            }

            var trimmed = identifier.Trim();
            if (!Permission.TryParseIdentifier(trimmed, out var parsed))
            {
                continue;
            }

            if (!PermissionLookup.TryGetValue(parsed.Canonical, out var permission))
            {
                continue;
            }

            if (!AreParametersValid(permission, parsed.Parameters, out _))
            {
                continue;
            }

            if (ScopeEvaluator.HasPermission(scope, parsed.Canonical, parsed.Parameters))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> HasAllPermissionsAsync(ClaimsPrincipal principal, IEnumerable<string> permissionIdentifiers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (permissionIdentifiers is null)
        {
            return false;
        }

        var identifiers = permissionIdentifiers
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToList();

        if (identifiers.Count == 0)
        {
            return false;
        }

        var rbacVersion = GetRbacVersion(principal);

        if (IsLegacyRbacVersion(rbacVersion))
        {
            // Legacy tokens get full admin access
            return true;
        }

        var scope = await ResolveScopeDirectivesAsync(principal, cancellationToken).ConfigureAwait(false);

        foreach (var identifier in identifiers)
        {
            if (!Permission.TryParseIdentifier(identifier, out var parsed))
            {
                return false;
            }

            if (!PermissionLookup.TryGetValue(parsed.Canonical, out var permission))
            {
                return false;
            }

            if (!AreParametersValid(permission, parsed.Parameters, out _))
            {
                return false;
            }

            if (!ScopeEvaluator.HasPermission(scope, parsed.Canonical, parsed.Parameters))
            {
                return false;
            }
        }

        return true;
    }

    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _tokenService.ValidateTokenPrincipalAsync(token, cancellationToken);
    }

    public async Task<TokenInfo?> DecodeTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _tokenService.DecodeTokenAsync(token, cancellationToken);
    }

    public async Task<string> MutateTokenAsync(
        string token,
        IEnumerable<string>? permissionsToAdd = null,
        IEnumerable<string>? permissionsToRemove = null,
        IEnumerable<Claim>? claimsToAdd = null,
        IEnumerable<Claim>? claimsToRemove = null,
        IEnumerable<string>? claimTypesToRemove = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var principal = await _tokenService.ValidateTokenPrincipalAsync(token, cancellationToken) ?? throw new SecurityTokenException("Token validation failed.");
        var existingPermissions = ExtractPermissionClaims(principal);

        var additions = new List<Claim>();
        var removals = new List<Claim>();
        var removalTypes = new HashSet<string>(StringComparer.Ordinal);
        var scopeAdditions = new List<string>();
        var scopeRemovals = new List<string>();

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
                    throw new ValidationException($"Claim type '{trimmed}' cannot be removed via mutation.");
                }

                removalTypes.Add(trimmed);
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
                    throw new ValidationException($"Claim type '{claim.Type}' cannot be removed via mutation.");
                }

                if (string.Equals(claim.Type, ScopeClaimType, StringComparison.Ordinal))
                {
                    throw new ValidationException("Scope claims must be removed via permissionsToRemove.");
                }

                removals.Add(CloneClaim(claim));
            }
        }

        if (permissionsToRemove is not null)
        {
            var normalizedRemovals = NormalizeAndValidate(permissionsToRemove, allowEmpty: true);
            foreach (var permission in normalizedRemovals)
            {
                scopeRemovals.Add(permission);
                existingPermissions.Remove(permission);
            }
        }

        if (permissionsToAdd is not null)
        {
            var normalizedAdditions = NormalizeAndValidate(permissionsToAdd, allowEmpty: true);
            foreach (var permission in normalizedAdditions)
            {
                if (existingPermissions.Add(permission))
                {
                    scopeAdditions.Add(permission);
                }
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
                    throw new ValidationException($"Claim type '{claim.Type}' cannot be added via mutation.");
                }

                if (string.Equals(claim.Type, ScopeClaimType, StringComparison.Ordinal))
                {
                    throw new ValidationException("Scope claims must be added via permissionsToAdd.");
                }

                var alreadyQueued = additions.Any(existing =>
                    string.Equals(existing.Type, claim.Type, StringComparison.Ordinal) &&
                    string.Equals(existing.Value, claim.Value, StringComparison.Ordinal) &&
                    string.Equals(existing.ValueType, claim.ValueType, StringComparison.Ordinal));

                if (!alreadyQueued)
                {
                    additions.Add(CloneClaim(claim));
                }
            }
        }

        var additionsList = additions.Count == 0 ? null : additions;
        var removalsList = removals.Count == 0 ? null : removals;
        var removalTypesList = removalTypes.Count == 0 ? null : removalTypes.ToArray();
        var scopeAdditionsList = scopeAdditions.Count == 0 ? null : scopeAdditions;
        var scopeRemovalsList = scopeRemovals.Count == 0 ? null : scopeRemovals;

        return await _tokenService.MutateTokenAsync(
            token: token,
            scopesToAdd: scopeAdditionsList,
            scopesToRemove: scopeRemovalsList,
            claimsToAdd: additionsList,
            claimsToRemove: removalsList,
            claimTypesToRemove: removalTypesList,
            expiration: expiration,
            cancellationToken: cancellationToken);
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

    private static string[] NormalizeAndValidate(IEnumerable<string> permissionIdentifiers, bool allowEmpty)
    {
        if (permissionIdentifiers is null)
        {
            return [];
        }

        var unique = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();

        foreach (var rawIdentifier in permissionIdentifiers)
        {
            if (string.IsNullOrWhiteSpace(rawIdentifier))
            {
                throw new ArgumentException("Permission identifiers cannot contain null or whitespace entries.", nameof(permissionIdentifiers));
            }

            Permission.ParsedIdentifier parsed;
            try
            {
                parsed = Permission.ParseIdentifier(rawIdentifier);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException($"Permission identifier '{rawIdentifier}' has an invalid format: {ex.Message}", nameof(permissionIdentifiers), ex);
            }

            if (!PermissionLookup.TryGetValue(parsed.Canonical, out var permission))
            {
                throw new ArgumentException($"Unknown permission identifier '{parsed.Identifier}'.", nameof(permissionIdentifiers));
            }

            if (permission.AccessCategory == PermissionAccessCategory.Unspecified)
            {
                throw new ArgumentException($"Permission identifier '{parsed.Identifier}' is not assignable. Select a specific scope or operation.", nameof(permissionIdentifiers));
            }

            if (!AreParametersValid(permission, parsed.Parameters, out var invalidParameter))
            {
                throw new ArgumentException($"Permission identifier '{parsed.Identifier}' specifies unsupported parameter '{invalidParameter}'.", nameof(permissionIdentifiers));
            }

            if (unique.Add(parsed.Identifier))
            {
                ordered.Add(parsed.Identifier);
            }
        }

        if (!allowEmpty && ordered.Count == 0)
        {
            throw new ArgumentException("At least one permission identifier must be provided.", nameof(permissionIdentifiers));
        }

        return ordered.Count == 0
            ? []
            : [.. ordered];
    }

    private static HashSet<string> ExtractPermissionClaims(ClaimsPrincipal principal)
    {
        var claimSet = new HashSet<string>(StringComparer.Ordinal);

        foreach (var claim in principal.Claims)
        {
            if (string.IsNullOrWhiteSpace(claim.Value))
            {
                continue;
            }

            if (string.Equals(claim.Type, ScopeClaimType, StringComparison.Ordinal))
            {
                var scopes = claim.Value.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (scopes.Length == 0)
                {
                    continue;
                }

                foreach (var scope in scopes)
                {
                    claimSet.Add(scope);
                }

                continue;
            }
        }

        // Legacy behavior: if no RBAC version, grant full access
        if (!principal.Claims.Any(claim => string.Equals(claim.Type, RbacVersionClaimType, StringComparison.Ordinal)))
        {
            claimSet.Add(Permissions.RootReadIdentifier);
            claimSet.Add(Permissions.RootWriteIdentifier);
        }

        return claimSet;
    }

    /// <summary>
    /// Extracts scope directives from the principal's claims.
    /// </summary>
    private static List<ScopeDirective> ExtractScopeDirectives(ClaimsPrincipal principal)
    {
        var directives = new List<ScopeDirective>();

        foreach (var claim in principal.Claims)
        {
            if (string.IsNullOrWhiteSpace(claim.Value))
            {
                continue;
            }

            if (!string.Equals(claim.Type, ScopeClaimType, StringComparison.Ordinal))
            {
                continue;
            }

            var scopes = claim.Value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var scope in scopes)
            {
                if (ScopeDirective.TryParse(scope, out var directive))
                {
                    directives.Add(directive!);
                }
            }
        }

        return directives;
    }

    /// <summary>
    /// Resolves scope directives by extracting role codes from the principal and looking up
    /// the current role definitions from the database. This allows role permission changes
    /// to take effect immediately without requiring token regeneration.
    /// </summary>
    private async Task<List<ScopeDirective>> ResolveScopeDirectivesAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var directives = new List<ScopeDirective>();

        // First, extract any direct scope claims (for backward compatibility)
        directives.AddRange(ExtractScopeDirectives(principal));

        // Parse role claims with inline parameters (format: "ROLE_CODE;param1=value1;param2=value2")
        // Support both short "role" claim type and verbose ClaimTypes.Role for compatibility
        var parsedRoles = principal.Claims
            .Where(c => string.Equals(c.Type, ClaimTypes.Role, StringComparison.Ordinal) ||
                        string.Equals(c.Type, "role", StringComparison.Ordinal))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => Role.TryParseRoleClaim(v, out var parsed) ? (Role.ParsedRoleClaim?)parsed : null)
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .ToList();

        if (parsedRoles.Count == 0)
        {
            return directives;
        }

        // Get unique role codes to look up
        var roleCodes = parsedRoles.Select(p => p.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Look up current role definitions from the database
        var roles = await _roleRepository.GetByCodesAsync(roleCodes, cancellationToken).ConfigureAwait(false);
        var roleIndex = roles.ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);

        // Expand scope templates for each parsed role claim
        foreach (var parsedRole in parsedRoles)
        {
            if (!roleIndex.TryGetValue(parsedRole.Code, out var role))
            {
                continue;
            }

            // Convert parsed parameters to string? dictionary for expansion
            var parameterValues = parsedRole.Parameters.ToDictionary(
                kvp => kvp.Key,
                kvp => (string?)kvp.Value,
                StringComparer.Ordinal);

            // Try to expand the role's scope templates with parameter values
            // If the role requires parameters that aren't provided, we'll expand
            // only the templates that don't require those parameters
            try
            {
                var roleDirectives = role.ExpandScope(parameterValues);
                directives.AddRange(roleDirectives);
            }
            catch (Domain.Shared.Exceptions.DomainException)
            {
                // Role requires parameters that weren't provided in the token
                // Try to expand individual templates that don't require missing parameters
                foreach (var template in role.ScopeTemplates)
                {
                    // Check if all required parameters are available
                    var missingParams = template.RequiredParameters
                        .Where(p => !parameterValues.ContainsKey(p) || parameterValues[p] is null)
                        .ToList();

                    if (missingParams.Count == 0)
                    {
                        // All parameters available, expand this template
                        try
                        {
                            var directive = template.Expand(parameterValues);
                            directives.Add(directive);
                        }
                        catch
                        {
                            // Skip templates that fail to expand
                        }
                    }
                    // else: Skip templates with missing parameters
                }
            }
        }

        return directives;
    }

    /// <summary>
    /// Gets the RBAC version from the principal's claims.
    /// </summary>
    private static string? GetRbacVersion(ClaimsPrincipal principal)
    {
        var claim = principal.Claims.FirstOrDefault(c =>
            string.Equals(c.Type, RbacVersionClaimType, StringComparison.Ordinal));

        return claim?.Value;
    }

    /// <summary>
    /// Determines if the RBAC version indicates a legacy token that should get full admin access.
    /// </summary>
    private static bool IsLegacyRbacVersion(string? version)
    {
        // null (missing) or "1" = legacy token → grant full admin access
        return string.IsNullOrEmpty(version) || string.Equals(version, "1", StringComparison.Ordinal);
    }

    private static bool HasPermissionInternal(HashSet<string> claimSet, Permission permission, Permission.ParsedIdentifier requestedIdentifier)
    {
        if (claimSet.Contains(Permissions.RootWriteIdentifier) &&
            (permission.AccessCategory == PermissionAccessCategory.Write || permission.AccessCategory == PermissionAccessCategory.Unspecified))
        {
            return true;
        }

        if (claimSet.Contains(Permissions.RootReadIdentifier) && permission.AccessCategory != PermissionAccessCategory.Write)
        {
            return true;
        }

        if (claimSet.Contains(requestedIdentifier.Identifier))
        {
            return true;
        }

        if (claimSet.Contains(permission.Path))
        {
            return true;
        }

        if (HasMatchingClaim(claimSet, permission.Path, requestedIdentifier.Parameters))
        {
            return true;
        }

        if (permission.AccessCategory is PermissionAccessCategory.Read or PermissionAccessCategory.Write)
        {
            var current = permission;
            while (current.Parent is not null)
            {
                var parent = current.Parent;
                if (permission.AccessCategory == PermissionAccessCategory.Read)
                {
                    if (ScopeSatisfied(claimSet, parent, PermissionAccessCategory.Read, requestedIdentifier.Parameters))
                    {
                        return true;
                    }
                }
                else if (permission.AccessCategory == PermissionAccessCategory.Write)
                {
                    if (ScopeSatisfied(claimSet, parent, PermissionAccessCategory.Write, requestedIdentifier.Parameters))
                    {
                        return true;
                    }
                }

                current = parent;
            }
        }

        return false;
    }

    private static bool ScopeSatisfied(
        HashSet<string> claimSet,
        Permission scopePermission,
        PermissionAccessCategory category,
        IReadOnlyDictionary<string, string> requestedParameters)
    {
        var scopePath = BuildScopePath(scopePermission.Path, category);

        var filteredParameters = FilterParameters(requestedParameters);

        if (filteredParameters is not null)
        {
            var scopedIdentifier = BuildScopedIdentifier(scopePermission, category, filteredParameters);
            if (claimSet.Contains(scopedIdentifier))
            {
                return true;
            }
        }

        if (claimSet.Contains(scopePath))
        {
            return true;
        }

        IReadOnlyDictionary<string, string> effectiveParameters;

        effectiveParameters = filteredParameters ?? EmptyParameterDictionary;

        return HasMatchingClaim(claimSet, scopePath, effectiveParameters);
    }

    private static bool HasMatchingClaim(
        HashSet<string> claimSet,
        string canonicalPath,
        IReadOnlyDictionary<string, string> requestedParameters)
    {
        foreach (var claim in claimSet)
        {
            if (!Permission.TryParseIdentifier(claim, out var parsedClaim))
            {
                continue;
            }

            if (!string.Equals(parsedClaim.Canonical, canonicalPath, StringComparison.Ordinal))
            {
                continue;
            }

            if (ParametersCompatible(parsedClaim.Parameters, requestedParameters))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ParametersCompatible(
        IReadOnlyDictionary<string, string> claimParameters,
        IReadOnlyDictionary<string, string> requestedParameters)
    {
        if (claimParameters.Count == 0)
        {
            return true;
        }

        foreach (var kvp in claimParameters)
        {
            if (!requestedParameters.TryGetValue(kvp.Key, out var requestedValue))
            {
                return false;
            }

            if (!string.Equals(kvp.Value, requestedValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static HashSet<string> CollectRelevantParameterNames(Permission permission)
        => new(permission.GetParameterHierarchy(), StringComparer.Ordinal);

    private static Dictionary<string, string>? FilterParameters(
        IReadOnlyDictionary<string, string> requestedParameters)
    {
        if (requestedParameters.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, string>(requestedParameters, StringComparer.Ordinal);
    }

    private static string BuildScopedIdentifier(
        Permission scopePermission,
        PermissionAccessCategory category,
        Dictionary<string, string> parameters)
    {
        string basePath;
        Dictionary<string, string?>? declaredValues = null;
        SortedDictionary<string, string>? extraValues = null;

        if (parameters.Count > 0)
        {
            var declaredNames = CollectRelevantParameterNames(scopePermission);

            foreach (var kvp in parameters)
            {
                if (declaredNames.Contains(kvp.Key))
                {
                    declaredValues ??= new Dictionary<string, string?>(StringComparer.Ordinal);
                    declaredValues[kvp.Key] = kvp.Value;
                }
                else
                {
                    extraValues ??= new SortedDictionary<string, string>(StringComparer.Ordinal);
                    extraValues[kvp.Key] = kvp.Value;
                }
            }
        }

        basePath = declaredValues is null
            ? scopePermission.Path
            : scopePermission.BuildPath(declaredValues);

        if (extraValues is not null)
        {
            var segment = string.Join(';', extraValues.Select(static kvp => $"{kvp.Key}={kvp.Value}"));
            basePath = $"{basePath}:[{segment}]";
        }

        return category switch
        {
            PermissionAccessCategory.Read => $"{basePath}:_read",
            PermissionAccessCategory.Write => $"{basePath}:_write",
            _ => basePath
        };
    }

    private static bool AreParametersValid(Permission permission, IReadOnlyDictionary<string, string> parameters, out string? invalidParameter)
    {
        if (parameters.Count == 0)
        {
            invalidParameter = null;
            return true;
        }

        if (permission.Identifier is "_read" or "_write" && permission.Parent is null)
        {
            invalidParameter = null;
            return true;
        }

        var allowedAncestors = CollectRelevantParameterNames(permission);
        var reachable = GetReachableParameterNames(permission);

        if (allowedAncestors.Count == 0 && reachable.Count == 0)
        {
            invalidParameter = null;
            return true;
        }

        foreach (var parameter in parameters.Keys)
        {
            if (allowedAncestors.Contains(parameter))
            {
                continue;
            }

            if (reachable.Contains(parameter))
            {
                continue;
            }

            invalidParameter = parameter;
            return false;
        }

        invalidParameter = null;
        return true;
    }

    private static string BuildScopePath(string basePath, PermissionAccessCategory category) => category switch
    {
        PermissionAccessCategory.Read => $"{basePath}:_read",
        PermissionAccessCategory.Write => $"{basePath}:_write",
        _ => basePath
    };

    private static ReadOnlyDictionary<string, HashSet<string>> BuildReachableParameterLookup(IEnumerable<Permission> roots)
    {
        var cache = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var root in roots)
        {
            if (root.Identifier is "_read" or "_write")
            {
                continue;
            }

            ComputeReachableParameters(root, cache);
        }

        foreach (var permission in roots.SelectMany(static root => root.Traverse()))
        {
            if (cache.ContainsKey(permission.Path))
            {
                continue;
            }

            if (permission.Identifier is "_read" or "_write")
            {
                var parent = permission.Parent;
                if (parent is not null && cache.TryGetValue(parent.Path, out var parentParameters))
                {
                    cache[permission.Path] = parentParameters;
                }
                else
                {
                    cache[permission.Path] = EmptyParameterNameSet;
                }
            }
            else
            {
                cache[permission.Path] = new HashSet<string>(permission.Parameters, StringComparer.Ordinal);
            }
        }

        return new ReadOnlyDictionary<string, HashSet<string>>(cache);
    }

    private static HashSet<string> ComputeReachableParameters(Permission permission, Dictionary<string, HashSet<string>> cache)
    {
        if (cache.TryGetValue(permission.Path, out var existing))
        {
            return existing;
        }

        var names = new HashSet<string>(permission.Parameters, StringComparer.Ordinal);

        foreach (var child in permission.Permissions)
        {
            if (child.Identifier is "_read" or "_write")
            {
                continue;
            }

            var childNames = ComputeReachableParameters(child, cache);
            names.UnionWith(childNames);
        }

        cache[permission.Path] = names;
        return names;
    }

    private static HashSet<string> GetReachableParameterNames(Permission permission)
    {
        if (ReachableParameterLookup.TryGetValue(permission.Path, out var parameters))
        {
            return parameters;
        }

        return EmptyParameterNameSet;
    }
}
