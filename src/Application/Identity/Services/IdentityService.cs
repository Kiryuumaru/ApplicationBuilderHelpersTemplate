using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Application.Authorization.Interfaces;
using Application.Identity.Interfaces;
using Application.Identity.Models;
using Domain.Authorization.Constants;
using Domain.Authorization.Models;
using Domain.Identity.Interfaces;
using Domain.Identity.Models;
using Domain.Identity.Services;
using Domain.Identity.ValueObjects;
using Microsoft.AspNetCore.Identity;
using RolesConstants = Domain.Authorization.Constants.Roles;

namespace Application.Identity.Services;

internal sealed class IdentityService(
    UserManager<User> userManager,
    IRoleRepository roleRepository,
    IUserRoleResolver roleResolver) : IIdentityService
{
    private static readonly Dictionary<string, Func<User, Role, string?>> DefaultRoleParameterResolvers =
        new(StringComparer.Ordinal)
        {
            [RoleIds.User.RoleUserIdParameter] = static (user, _) => user.Id.ToString()
        };

    private readonly UserManager<User> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    private readonly IRoleRepository _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
    private readonly IUserRoleResolver _roleResolver = roleResolver ?? throw new ArgumentNullException(nameof(roleResolver));

    public async Task<User> RegisterUserAsync(UserRegistrationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ArgumentException("Username cannot be blank.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Password cannot be blank.", nameof(request));
        }

        var user = User.Register(request.Username, request.Email);

        if (request.PermissionIdentifiers is { Count: > 0 })
        {
            foreach (var identifier in request.PermissionIdentifiers)
            {
                if (string.IsNullOrWhiteSpace(identifier))
                {
                    continue;
                }

                user.GrantPermission(UserPermissionGrant.Create(identifier));
            }
        }

        if (request.AutoActivate)
        {
            user.Activate();
        }

        var suppliedAssignments = request.RoleAssignments;
        if (suppliedAssignments is { Count: > 0 })
        {
            foreach (var assignment in suppliedAssignments)
            {
                await AssignRoleInternalAsync(user, assignment, cancellationToken).ConfigureAwait(false);
            }
        }

        var hasUserRoleAssignment = suppliedAssignments?.Any(static assignment =>
            assignment is not null &&
            !string.IsNullOrWhiteSpace(assignment.RoleCode) &&
            string.Equals(assignment.RoleCode, RolesConstants.User.Code, StringComparison.OrdinalIgnoreCase)) == true;

        if (!hasUserRoleAssignment)
        {
            await AssignRoleInternalAsync(user, new RoleAssignmentRequest(RolesConstants.User.Code), cancellationToken).ConfigureAwait(false);
        }

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"User creation failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    public async Task<User> RegisterExternalAsync(ExternalUserRegistrationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Provider))
        {
            throw new ArgumentException("Provider cannot be blank.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.ProviderSubject))
        {
            throw new ArgumentException("Subject cannot be blank.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ArgumentException("Username cannot be blank.", nameof(request));
        }

        // Check if user exists by login
        var existingUser = await _userManager.FindByLoginAsync(request.Provider, request.ProviderSubject);
        if (existingUser != null)
        {
             throw new InvalidOperationException($"User with provider '{request.Provider}' and subject '{request.ProviderSubject}' already exists.");
        }

        var user = User.Register(request.Username, request.ProviderEmail);
        
        // External users are usually auto-activated or handled differently
        user.Activate();

        // Assign default role
        await AssignRoleInternalAsync(user, new RoleAssignmentRequest(RolesConstants.User.Code), cancellationToken).ConfigureAwait(false);

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"User creation failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        var loginResult = await _userManager.AddLoginAsync(user, new UserLoginInfo(request.Provider, request.ProviderSubject, request.Provider));
        if (!loginResult.Succeeded)
        {
             // Cleanup?
             await _userManager.DeleteAsync(user);
             throw new InvalidOperationException($"Adding external login failed: {string.Join(", ", loginResult.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _userManager.FindByNameAsync(username);
    }

    public Task<IReadOnlyCollection<User>> ListAsync(CancellationToken cancellationToken)
    {
        // UserManager doesn't have a direct ListAsync, usually via IQueryable
        // For now, throwing NotImplemented or we need to expose IQueryable from Store
        throw new NotImplementedException();
    }

    public async Task<UserSession> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be blank.", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be blank.", nameof(password));
        }

        var user = await _userManager.FindByNameAsync(username);
        if (user == null || !await _userManager.CheckPasswordAsync(user, password))
        {
            throw new AuthenticationException("Invalid credentials.");
        }

        // We need to create a session. 
        // Note: Identity usually uses Cookies/Tokens. UserSession might be a custom concept.
        // If we keep UserSession, we need to generate it.
        
        var resolvedRoles = _roleResolver.ResolveRoles(user);
        var roleCodes = resolvedRoles.Count == 0
            ? null
            : resolvedRoles.Select(static resolution => resolution.Role.Code);
        var permissions = user.BuildEffectivePermissions(resolvedRoles);
        
        // Assuming default lifetime
        return user.CreateSession(TimeSpan.FromHours(1), DateTimeOffset.UtcNow, permissions, roleCodes);
    }

    public async Task AssignRoleAsync(Guid userId, RoleAssignmentRequest assignment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        cancellationToken.ThrowIfCancellationRequested();
        
        // UserId is Guid, UserManager expects string usually, but our UserStore handles Guid <-> String
        var user = await _userManager.FindByIdAsync(userId.ToString()) ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        await AssignRoleInternalAsync(user, assignment, cancellationToken).ConfigureAwait(false);
        await _userManager.UpdateAsync(user);
    }

    private async Task AssignRoleInternalAsync(User user, RoleAssignmentRequest assignment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(assignment);
        if (string.IsNullOrWhiteSpace(assignment.RoleCode))
        {
            throw new ArgumentException("Role code cannot be blank.", nameof(assignment));
        }

        var roleCode = assignment.RoleCode.Trim();
        var role = await _roleRepository.GetByCodeAsync(roleCode, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Role '{roleCode}' was not found.");

        var parameters = ResolveAssignmentParameters(user, role, assignment);
        user.AssignRole(role.Id, parameters);
    }

    private static Dictionary<string, string?>? ResolveAssignmentParameters(User user, Role role, RoleAssignmentRequest assignment)
    {
        var requiredParameters = role.PermissionGrants
            .Where(static template => template.RequiresParameters)
            .SelectMany(static template => template.RequiredParameters)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var resolved = NormalizeParameters(assignment.ParameterValues);
        if (requiredParameters.Length == 0)
        {
            return resolved.Count == 0 ? null : resolved;
        }

        foreach (var parameter in requiredParameters)
        {
            if (resolved.TryGetValue(parameter, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (TryResolveDefaultParameter(parameter, user, role, out var defaultValue) && !string.IsNullOrWhiteSpace(defaultValue))
            {
                resolved[parameter] = defaultValue;
                continue;
            }

            throw new InvalidOperationException($"Role '{role.Code}' requires parameter '{parameter}'.");
        }

        return resolved;
    }

    private static Dictionary<string, string?> NormalizeParameters(IReadOnlyDictionary<string, string?>? parameterValues)
    {
        if (parameterValues is null || parameterValues.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        var normalized = new Dictionary<string, string?>(parameterValues.Count, StringComparer.Ordinal);
        foreach (var kvp in parameterValues)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value))
            {
                continue;
            }

            normalized[kvp.Key.Trim()] = kvp.Value!.Trim();
        }

        return normalized;
    }

    private static bool TryResolveDefaultParameter(string parameterName, User user, Role role, out string? value)
    {
        if (DefaultRoleParameterResolvers.TryGetValue(parameterName, out var resolver))
        {
            value = resolver(user, role);
            return value is not null;
        }

        value = null;
        return false;
    }
}
