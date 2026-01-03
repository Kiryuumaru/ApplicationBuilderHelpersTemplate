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
using Domain.Identity.Models;
using Domain.Identity.Services;
using Domain.Identity.ValueObjects;
using Microsoft.AspNetCore.Identity;
using RolesConstants = Domain.Authorization.Constants.Roles;

namespace Application.Identity.Services;

internal sealed class IdentityService(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IRoleRepository roleRepository,
    IUserRoleResolver roleResolver,
    IUserStore userStore,
    UserAuthenticationService authService) : IIdentityService
{
    private static readonly Dictionary<string, Func<User, Role, string?>> DefaultRoleParameterResolvers =
        new(StringComparer.Ordinal)
        {
            [RoleIds.User.RoleUserIdParameter] = static (user, _) => user.Id.ToString()
        };

    private readonly UserManager<User> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    private readonly SignInManager<User> _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
    private readonly IRoleRepository _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
    private readonly IUserRoleResolver _roleResolver = roleResolver ?? throw new ArgumentNullException(nameof(roleResolver));
    private readonly IUserStore _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
    private readonly UserAuthenticationService _authService = authService ?? throw new ArgumentNullException(nameof(authService));

    public async Task<User> RegisterUserAsync(UserRegistrationRequest? request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Anonymous registration when request is null
        if (request is null)
        {
            var anonymousUser = User.RegisterAnonymous();
            anonymousUser.Activate();

            var anonymousResult = await _userManager.CreateAsync(anonymousUser);
            if (!anonymousResult.Succeeded)
            {
                throw new InvalidOperationException($"Anonymous user creation failed: {string.Join(", ", anonymousResult.Errors.Select(e => e.Description))}");
            }

            return anonymousUser;
        }

        // Regular user registration
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

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _userManager.FindByEmailAsync(email);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _userManager.FindByIdAsync(id.ToString());
    }

    public async Task<IReadOnlyCollection<User>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _userStore.ListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserSession> AuthenticateAsync(string usernameOrEmail, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(usernameOrEmail))
        {
            throw new ArgumentException("Username or email cannot be blank.", nameof(usernameOrEmail));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be blank.", nameof(password));
        }

        // Try to find user by username first
        var user = await _userManager.FindByNameAsync(usernameOrEmail);
        
        // If not found and looks like an email, try finding by email
        if (user is null && usernameOrEmail.Contains('@'))
        {
            user = await _userManager.FindByEmailAsync(usernameOrEmail);
        }
        
        if (user is null)
        {
            throw new AuthenticationException("Invalid credentials.");
        }

        // Anonymous users cannot login with password
        if (user.IsAnonymous)
        {
            throw new AuthenticationException("Invalid credentials.");
        }

        try
        {
            _authService.Authenticate(user, password);
        }
        catch (Domain.Identity.Exceptions.AuthenticationException ex)
        {
            await _userManager.UpdateAsync(user).ConfigureAwait(false);
            throw new AuthenticationException(ex.Message, ex);
        }

        var resolvedRoles = await _roleResolver.ResolveRolesAsync(user, cancellationToken).ConfigureAwait(false);
        var roleCodes = resolvedRoles.Count == 0
            ? Array.Empty<string>()
            : resolvedRoles.Select(static resolution => resolution.Role.Code).ToArray();
        var scopeDirectives = user.BuildEffectiveScopeDirectives(resolvedRoles);

        await _userManager.UpdateAsync(user).ConfigureAwait(false);

        return user.CreateScopedSession(TimeSpan.FromHours(1), scopeDirectives, DateTimeOffset.UtcNow, roleCodes);
    }

    public async Task<UserSession> CreateSessionForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");

        var resolvedRoles = await _roleResolver.ResolveRolesAsync(user, cancellationToken).ConfigureAwait(false);
        var roleCodes = resolvedRoles.Count == 0
            ? Array.Empty<string>()
            : resolvedRoles.Select(static resolution => resolution.Role.Code).ToArray();
        var scopeDirectives = user.BuildEffectiveScopeDirectives(resolvedRoles);

        return user.CreateScopedSession(TimeSpan.FromHours(1), scopeDirectives, DateTimeOffset.UtcNow, roleCodes);
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
        Role role;
        if (RolesConstants.TryGetByCode(roleCode, out var staticRole))
        {
            role = staticRole;
        }
        else
        {
            role = await _roleRepository.GetByCodeAsync(roleCode, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Role '{roleCode}' was not found.");
        }

        var parameters = ResolveAssignmentParameters(user, role, assignment);
        user.AssignRole(role.Id, parameters);
    }

    private static Dictionary<string, string?>? ResolveAssignmentParameters(User user, Role role, RoleAssignmentRequest assignment)
    {
        var requiredParameters = role.ScopeTemplates
            .Where(static template => template.RequiredParameters.Count > 0)
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

    public async Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = await _userManager.FindByIdAsync(userId.ToString()) 
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        
        if (!user.RemoveRole(roleId))
        {
            throw new InvalidOperationException($"User does not have role '{roleId}'.");
        }
        
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to update user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task UpdateUserAsync(Guid userId, UserUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = await _userManager.FindByIdAsync(userId.ToString()) 
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        
        if (request.Email is not null)
        {
            user.SetEmail(request.Email);
        }
        
        if (request.PhoneNumber is not null)
        {
            user.SetPhoneNumber(request.PhoneNumber);
        }
        
        if (request.LockoutEnabled.HasValue)
        {
            user.SetLockoutEnabled(request.LockoutEnabled.Value);
        }
        
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to update user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = await _userManager.FindByIdAsync(userId.ToString()) 
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to delete user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            throw new ArgumentException("New password cannot be blank.", nameof(newPassword));
        }
        
        var user = await _userManager.FindByIdAsync(userId.ToString()) 
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        
        // Remove existing password and add new one
        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to remove password: {string.Join(", ", removeResult.Errors.Select(e => e.Description))}");
        }
        
        var addResult = await _userManager.AddPasswordAsync(user, newPassword);
        if (!addResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to set password: {string.Join(", ", addResult.Errors.Select(e => e.Description))}");
        }
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            throw new ArgumentException("Current password cannot be blank.", nameof(currentPassword));
        }
        
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            throw new ArgumentException("New password cannot be blank.", nameof(newPassword));
        }
        
        var user = await _userManager.FindByIdAsync(userId.ToString()) 
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        
        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            // Check if it's specifically a wrong password error
            if (result.Errors.Any(e => e.Code == "PasswordMismatch"))
            {
                throw new UnauthorizedAccessException("Current password is incorrect.");
            }
            throw new InvalidOperationException($"Failed to change password: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = await _userManager.FindByIdAsync(userId.ToString()) 
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        
        var resolvedRoles = await _roleResolver.ResolveRolesAsync(user, cancellationToken).ConfigureAwait(false);
        return user.BuildEffectivePermissions(resolvedRoles);
    }

    public async Task<TwoFactorSetupInfo> Setup2faAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = await _userManager.FindByIdAsync(userId.ToString()) 
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        
        // Get or generate authenticator key
        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        if (string.IsNullOrEmpty(unformattedKey))
        {
            throw new InvalidOperationException("Failed to generate authenticator key.");
        }

        var formattedKey = FormatAuthenticatorKey(unformattedKey);
        var email = user.Email ?? user.UserName ?? userId.ToString();
        var authenticatorUri = GenerateAuthenticatorUri("ProjectOffworlder", email, unformattedKey);

        return new TwoFactorSetupInfo(unformattedKey, authenticatorUri, formattedKey);
    }

    public async Task<IReadOnlyCollection<string>> Enable2faAsync(Guid userId, string verificationCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (string.IsNullOrWhiteSpace(verificationCode))
        {
            throw new ArgumentException("Verification code is required.", nameof(verificationCode));
        }
        
        var user = await _userManager.FindByIdAsync(userId.ToString()) 
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        
        // Strip spaces and hyphens from code
        var code = verificationCode.Replace(" ", string.Empty).Replace("-", string.Empty);
        
        // Verify the code
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);
        
        if (!isValid)
        {
            throw new InvalidOperationException("Invalid verification code.");
        }
        
        // Enable 2FA
        var result = await _userManager.SetTwoFactorEnabledAsync(user, true);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to enable 2FA: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
        
        // Generate recovery codes
        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return recoveryCodes?.ToList() ?? [];
    }

    public async Task Disable2faAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = await _userManager.FindByIdAsync(userId.ToString()) 
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        
        if (!await _userManager.GetTwoFactorEnabledAsync(user))
        {
            throw new InvalidOperationException("2FA is not currently enabled for this user.");
        }
        
        var result = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to disable 2FA: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task<bool> Verify2faCodeAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }
        
        var user = await _userManager.FindByIdAsync(userId.ToString()) 
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        
        var sanitizedCode = code.Replace(" ", string.Empty).Replace("-", string.Empty);
        
        return await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, sanitizedCode);
    }

    public async Task<AuthenticationResult> AuthenticateWithResultAsync(string usernameOrEmail, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // Try to find user by username first
        var user = await _userManager.FindByNameAsync(usernameOrEmail);
        
        // If not found and looks like an email, try finding by email
        if (user is null && usernameOrEmail.Contains('@'))
        {
            user = await _userManager.FindByEmailAsync(usernameOrEmail);
        }
        
        if (user is null)
        {
            return AuthenticationResult.Failed();
        }

        // Anonymous users cannot login with password
        if (user.IsAnonymous)
        {
            return AuthenticationResult.Failed();
        }
        
        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            return AuthenticationResult.Failed();
        }
        
        // Check if 2FA is enabled
        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return AuthenticationResult.TwoFactorRequired(user.Id);
        }
        
        // No 2FA, create session directly
        var resolvedRoles = await _roleResolver.ResolveRolesAsync(user, cancellationToken);
        var roleCodes = resolvedRoles.Count == 0
            ? Array.Empty<string>()
            : resolvedRoles.Select(static resolution => resolution.Role.Code).ToArray();
        var scopeDirectives = user.BuildEffectiveScopeDirectives(resolvedRoles);
        var session = user.CreateScopedSession(TimeSpan.FromHours(1), scopeDirectives, DateTimeOffset.UtcNow, roleCodes);
        
        return AuthenticationResult.Success(session);
    }

    public async Task<UserSession> Complete2faAuthenticationAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = await _userManager.FindByIdAsync(userId.ToString()) 
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        
        var sanitizedCode = code.Replace(" ", string.Empty).Replace("-", string.Empty);
        
        // Try TOTP code first
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, sanitizedCode);
        
        if (!isValid)
        {
            // Try recovery code
            var redemptionResult = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, sanitizedCode);
            if (!redemptionResult.Succeeded)
            {
                throw new UnauthorizedAccessException("Invalid 2FA code.");
            }
        }
        
        var resolvedRoles = await _roleResolver.ResolveRolesAsync(user, cancellationToken);
        var roleCodes = resolvedRoles.Count == 0
            ? Array.Empty<string>()
            : resolvedRoles.Select(static resolution => resolution.Role.Code).ToArray();
        var scopeDirectives = user.BuildEffectiveScopeDirectives(resolvedRoles);
        return user.CreateScopedSession(TimeSpan.FromHours(1), scopeDirectives, DateTimeOffset.UtcNow, roleCodes);
    }

    public async Task<IReadOnlyCollection<string>> GenerateRecoveryCodesAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = await _userManager.FindByIdAsync(userId.ToString()) 
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        
        var isTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
        if (!isTwoFactorEnabled)
        {
            throw new InvalidOperationException("Cannot generate recovery codes because 2FA is not enabled.");
        }
        
        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return recoveryCodes?.ToArray() ?? [];
    }

    public async Task<int> GetRecoveryCodeCountAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = await _userManager.FindByIdAsync(userId.ToString()) 
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        
        return await _userManager.CountRecoveryCodesAsync(user);
    }

    private static string FormatAuthenticatorKey(string unformattedKey)
    {
        var result = new System.Text.StringBuilder();
        int currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }
        return result.ToString().ToLowerInvariant();
    }

    private static string GenerateAuthenticatorUri(string issuer, string email, string secret)
    {
        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits=6";
    }

    public async Task LinkPasswordAsync(Guid userId, string username, string password, string? email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");

        // If user is anonymous, upgrade them
        if (user.IsAnonymous)
        {
            user.UpgradeFromAnonymous(username);
            
            // Assign the default user role
            await AssignRoleInternalAsync(user, new RoleAssignmentRequest(RolesConstants.User.Code), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // For non-anonymous users, update username if different
            if (!string.Equals(user.UserName, username, StringComparison.OrdinalIgnoreCase))
            {
                user.SetUserName(username);
            }
        }

        // Update email if provided
        if (!string.IsNullOrWhiteSpace(email))
        {
            user.SetEmail(email);
        }

        // Add the password
        var addPasswordResult = await _userManager.AddPasswordAsync(user, password);
        if (!addPasswordResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to add password: {string.Join(", ", addPasswordResult.Errors.Select(e => e.Description))}");
        }

        await _userManager.UpdateAsync(user);
    }

    public async Task LinkEmailAsync(Guid userId, string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be blank.", nameof(email));
        }

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");

        // Check if email is already linked to another user
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null && existingUser.Id != userId)
        {
            throw new InvalidOperationException($"Email '{email}' is already registered to another account.");
        }

        // Check if user already has this email
        if (string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            return; // No-op if same email
        }

        user.SetEmail(email);
        user.SetEmailConfirmed(false); // Require re-verification for new email

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to link email: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task ChangeUsernameAsync(Guid userId, string newUsername, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(newUsername))
        {
            throw new ArgumentException("Username cannot be blank.", nameof(newUsername));
        }

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");

        // Anonymous users cannot change username (they don't have one)
        if (user.IsAnonymous)
        {
            throw new InvalidOperationException("Anonymous users cannot change username. Link a password or OAuth first.");
        }

        // Check if username is already taken
        if (!string.Equals(user.UserName, newUsername, StringComparison.OrdinalIgnoreCase))
        {
            var existingUser = await _userManager.FindByNameAsync(newUsername);
            if (existingUser is not null)
            {
                throw new InvalidOperationException($"Username '{newUsername}' is already taken.");
            }
        }
        else
        {
            return; // No-op if same username (case-insensitive)
        }

        user.SetUserName(newUsername);
        user.SetNormalizedUserName(newUsername.ToUpperInvariant());

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to change username: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task ChangeEmailAsync(Guid userId, string newEmail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(newEmail))
        {
            throw new ArgumentException("Email cannot be blank.", nameof(newEmail));
        }

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");

        // Check if email is already registered to another user
        var existingUser = await _userManager.FindByEmailAsync(newEmail);
        if (existingUser is not null && existingUser.Id != userId)
        {
            throw new InvalidOperationException($"Email '{newEmail}' is already registered to another account.");
        }

        // No-op if same email
        if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        user.SetEmail(newEmail);
        user.SetEmailConfirmed(false); // Require re-verification for new email

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to change email: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task UnlinkEmailAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");

        // Check if user has an email to unlink
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException("No email is linked to this account.");
        }

        user.SetEmail(null);
        user.SetEmailConfirmed(false);

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to unlink email: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task UpgradeAnonymousWithPasskeyAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");

        if (!user.IsAnonymous)
        {
            // Already non-anonymous, nothing to do
            return;
        }

        user.UpgradeFromAnonymousWithPasskey();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to upgrade anonymous user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task<string?> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Don't reveal that the user doesn't exist
            return null;
        }
        
        // For production, you might also want to check if email is confirmed:
        // if (!await _userManager.IsEmailConfirmedAsync(user)) return null;
        
        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<bool> ResetPasswordWithTokenAsync(string email, string token, string newPassword, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Don't reveal that the user doesn't exist
            return false;
        }
        
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded;
    }
}
