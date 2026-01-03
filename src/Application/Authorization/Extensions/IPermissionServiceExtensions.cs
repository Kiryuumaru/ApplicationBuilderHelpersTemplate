using Application.Authorization.Interfaces;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Authorization.Extensions;

public static class IPermissionServiceExtensions
{
    public static Task<string> GenerateTokenWithPermissionsAsync(
        this IPermissionService permissionService,
        string userId,
        string username,
        IEnumerable<string> permissionIdentifiers,
        IEnumerable<Claim>? additionalClaims = null,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) =>
        permissionService.GenerateTokenWithPermissionsAsync(
            userId: userId,
            username: username,
            permissionIdentifiers: permissionIdentifiers,
            additionalClaims: additionalClaims,
            expiration: expiration.HasValue ? DateTimeOffset.UtcNow.Add(expiration.Value) : null,
            cancellationToken: cancellationToken);

    public static Task<string> GenerateApiKeyTokenWithPermissionsAsync(
        this IPermissionService permissionService,
        string apiKeyName,
        IEnumerable<string> permissionIdentifiers,
        IEnumerable<Claim>? additionalClaims = null,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) =>
        permissionService.GenerateApiKeyTokenWithPermissionsAsync(
            apiKeyName: apiKeyName,
            permissionIdentifiers: permissionIdentifiers,
            additionalClaims: additionalClaims,
            expiration: expiration.HasValue ? DateTimeOffset.UtcNow.Add(expiration.Value) : null,
            cancellationToken: cancellationToken);

    public static Task<string> MutateTokenAsync(
        this IPermissionService permissionService,
        string token,
        IEnumerable<string>? permissionsToAdd = null,
        IEnumerable<string>? permissionsToRemove = null,
        IEnumerable<Claim>? claimsToAdd = null,
        IEnumerable<Claim>? claimsToRemove = null,
        IEnumerable<string>? claimTypesToRemove = null,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) =>
        permissionService.MutateTokenAsync(
            token: token,
            permissionsToAdd: permissionsToAdd,
            permissionsToRemove: permissionsToRemove,
            claimsToAdd: claimsToAdd,
            claimsToRemove: claimsToRemove,
            claimTypesToRemove: claimTypesToRemove,
            expiration: expiration.HasValue ? DateTimeOffset.UtcNow.Add(expiration.Value) : null,
            cancellationToken: cancellationToken);
}
