using System;
using System.Collections.Generic;
using System.Linq;
using Application.Authorization.Roles.Interfaces;
using Application.Identity.Interfaces;
using Application.Identity.Models;
using Domain.Authorization.Constants;
using Domain.Authorization.Models;
using Domain.Authorization.ValueObjects;
using Domain.Identity.Interfaces;
using Domain.Identity.Models;
using Domain.Identity.Services;
using Domain.Identity.ValueObjects;
using RolesConstants = Domain.Authorization.Constants.Roles;

namespace Application.Identity.Services;

internal sealed class IdentityService : IIdentityService
{
    private static readonly IReadOnlyDictionary<string, Func<User, Role, string?>> DefaultRoleParameterResolvers =
        new Dictionary<string, Func<User, Role, string?>>(StringComparer.Ordinal)
        {
            [RoleIds.User.RoleUserIdParameter] = static (user, _) => user.Id.ToString()
        };

    private readonly IUserStore _userStore;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordCredentialFactory _credentialFactory;
    private readonly IUserSecretValidator _secretValidator;
    private readonly IUserRoleResolver _roleResolver;

    public IdentityService(
        IUserStore userStore,
        IRoleRepository roleRepository,
        IPasswordCredentialFactory credentialFactory,
        IUserSecretValidator secretValidator,
        IUserRoleResolver roleResolver)
    {
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _credentialFactory = credentialFactory ?? throw new ArgumentNullException(nameof(credentialFactory));
        _secretValidator = secretValidator ?? throw new ArgumentNullException(nameof(secretValidator));
        _roleResolver = roleResolver ?? throw new ArgumentNullException(nameof(roleResolver));
    }

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

        var existing = await _userStore.FindByUsernameAsync(request.Username, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new InvalidOperationException($"User '{request.Username}' already exists.");
        }

        var credential = _credentialFactory.Create(request.Password);
        var user = User.Register(request.Username, request.Email, credential);

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

        await _userStore.SaveAsync(user, cancellationToken).ConfigureAwait(false);
        return user;
    }

    public async Task<User> RegisterExternalAsync(ExternalUserRegistrationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ArgumentException("Username cannot be blank.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Provider))
        {
            throw new ArgumentException("Provider cannot be blank.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ProviderSubject))
        {
            throw new ArgumentException("Provider subject cannot be blank.", nameof(request));
        }

        var existing = await _userStore.FindByUsernameAsync(request.Username, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new InvalidOperationException($"User '{request.Username}' already exists.");
        }

        var normalizedProvider = request.Provider.Trim().ToLowerInvariant();
        var normalizedSubject = request.ProviderSubject.Trim();

        var linked = await _userStore.FindByExternalIdentityAsync(normalizedProvider, normalizedSubject, cancellationToken).ConfigureAwait(false);
        if (linked is not null)
        {
            throw new InvalidOperationException($"External identity '{normalizedProvider}:{normalizedSubject}' is already linked.");
        }

        var user = User.RegisterExternal(
            request.Username,
            normalizedProvider,
            normalizedSubject,
            request.ProviderEmail,
            request.ProviderDisplayName,
            request.Email);

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

        await _userStore.SaveAsync(user, cancellationToken).ConfigureAwait(false);
        return user;
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _userStore.FindByUsernameAsync(username, cancellationToken);
    }

    public Task<IReadOnlyCollection<User>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _userStore.ListAsync(cancellationToken);
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

        var user = await _userStore.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"User '{username}' was not found.");

        var authService = new UserAuthenticationService(_secretValidator, _roleResolver);
        var session = authService.Authenticate(user, password.AsSpan(), DateTimeOffset.UtcNow);
        await _userStore.SaveAsync(user, cancellationToken).ConfigureAwait(false);
        return session;
    }

    public async Task AssignRoleAsync(Guid userId, RoleAssignmentRequest assignment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userStore.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");

        await AssignRoleInternalAsync(user, assignment, cancellationToken).ConfigureAwait(false);
        await _userStore.SaveAsync(user, cancellationToken).ConfigureAwait(false);
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

    private static IReadOnlyDictionary<string, string?>? ResolveAssignmentParameters(User user, Role role, RoleAssignmentRequest assignment)
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
