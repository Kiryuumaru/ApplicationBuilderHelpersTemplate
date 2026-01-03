# TODO: Auth & Identity - DDD and Clean Architecture Improvements

## Current Problems

### 1. God Service Anti-Pattern
`IIdentityService` has 25 methods handling too many responsibilities.

### 2. ASP.NET Identity Coupling
Application layer directly uses `UserManager<User>` and `SignInManager<User>`.

### 3. Granular Stores (Not Aggregates)
5 separate store interfaces instead of repositories per aggregate root.

### 4. Infrastructure Interfaces in Application
JWT, HTTP client, and factory interfaces don't belong in Application layer.

### 5. Anemic Domain Model
Business rules scattered in Application services instead of Domain.

### 6. No Access Control on Repositories
Repositories are public and can be injected by any consumer, breaking encapsulation.

### 7. Leaking Domain Entities
Public services return mutable `User` entities. Consumers can modify entities directly, bypassing business rules.

---

## Target Folder Structure

```
Application/
├── Identity/
│   ├── Interfaces/                              # namespace Application.Identity.Interfaces
│   │   ├── IUserRegistrationService.cs          # public
│   │   ├── IAuthenticationService.cs            # public
│   │   ├── IPasswordService.cs                  # public
│   │   ├── ITwoFactorService.cs                 # public
│   │   ├── IUserProfileService.cs               # public
│   │   ├── IUserRoleService.cs                  # public
│   │   ├── IPasskeyService.cs                   # public (keep)
│   │   ├── IOAuthService.cs                     # public (keep)
│   │   ├── IAnonymousUserCleanupService.cs      # public (keep)
│   │   │
│   │   └── Internal/                            # namespace Application.Identity.Interfaces.Internal
│   │       ├── IUserRepository.cs               # internal
│   │       ├── ISessionRepository.cs            # internal
│   │       └── IPasskeyRepository.cs            # internal
│   │
│   ├── Models/                                  # namespace Application.Identity.Models
│   │   ├── UserRegistrationRequest.cs
│   │   ├── AuthenticationResult.cs
│   │   ├── UserDto.cs                           # Read-only DTO for User
│   │   ├── UserSessionDto.cs                    # Read-only DTO for Session
│   │   └── ...
│   │
│   └── Services/                                # namespace Application.Identity.Services
│       ├── UserRegistrationService.cs           # implements IUserRegistrationService
│       ├── AuthenticationService.cs             # implements IAuthenticationService
│       └── ...
│
├── Authorization/
│   ├── Interfaces/                              # namespace Application.Authorization.Interfaces
│   │   ├── IRoleService.cs                      # public (keep)
│   │   ├── IPermissionService.cs                # public (keep)
│   │   ├── IUserRoleResolver.cs                 # public (keep)
│   │   ├── ITokenService.cs                     # public (new - abstract)
│   │   │
│   │   └── Internal/                            # namespace Application.Authorization.Interfaces.Internal
│   │       └── IRoleRepository.cs               # internal
│   │
│   ├── Models/
│   │   └── ...
│   │
│   └── Services/
│       └── ...
│
└── AssemblyInfo.cs                              # InternalsVisibleTo declarations
```

## Assembly Access Control

```csharp
// Application/AssemblyInfo.cs
[assembly: InternalsVisibleTo("Infrastructure.EFCore")]
[assembly: InternalsVisibleTo("Infrastructure.EFCore.Identity")]
[assembly: InternalsVisibleTo("Application.UnitTests")]
[assembly: InternalsVisibleTo("Application.IntegrationTests")]
```

## Consumer Usage

```csharp
// ✅ Consumers can inject public services
using Application.Identity.Interfaces;

public class OrderController(
    IUserProfileService userProfile,      // ✅ public - allowed
    IAuthenticationService auth           // ✅ public - allowed
) { }

// ❌ Consumers CANNOT inject internal repositories
using Application.Identity.Interfaces.Internal;  // Namespace exists but...

public class OrderController(
    IUserRepository repo                  // ❌ Compile error - internal
) { }
```

---

## Target Interfaces

### Application/Identity/Interfaces/ (public)

| Interface | Methods | Source |
|-----------|---------|--------|
| `IUserRegistrationService` | 4 | Split from IIdentityService |
| `IAuthenticationService` | 4 | Split from IIdentityService |
| `IPasswordService` | 5 | Split from IIdentityService |
| `ITwoFactorService` | 6 | Split from IIdentityService |
| `IUserProfileService` | 9 | Split from IIdentityService |
| `IUserRoleService` | 3 | Split from IIdentityService |
| `IPasskeyService` | 7 | Keep as-is |
| `IOAuthService` | 5 | Keep as-is |
| `IAnonymousUserCleanupService` | 1 | Keep as-is |

### Application/Identity/Interfaces/Internal/ (internal)

| Interface | Source |
|-----------|--------|
| `IUserRepository` | Merge: IUserStore + IExternalLoginStore |
| `ISessionRepository` | Rename: ISessionStore |
| `IPasskeyRepository` | Merge: IPasskeyCredentialStore + IPasskeyChallengeStore |

### Application/Authorization/Interfaces/ (public)

| Interface | Action |
|-----------|--------|
| `IRoleService` | Keep |
| `IPermissionService` | Keep |
| `IUserRoleResolver` | Keep |
| `ITokenService` | New - abstract token operations |

### Application/Authorization/Interfaces/Internal/ (internal)

| Interface | Action |
|-----------|--------|
| `IRoleRepository` | Keep (absorb IRoleLookup) |

### Move to Infrastructure

| Interface | Move To |
|-----------|---------|
| `IJwtTokenService` | Infrastructure.Jwt |
| `IJwtTokenServiceFactory` | Infrastructure.Jwt |
| `IAuthorizedHttpClientFactory` | Infrastructure |

### Delete

| Interface | Reason |
|-----------|--------|
| `IRoleLookup` | Duplicate of IRoleRepository |
| `IPermissionServiceFactory` | Infra wiring detail |

---

## Detailed Interface Definitions

### DTOs (namespace Application.Identity.Models)

Public services return **DTOs (read-only)**, not domain entities. This prevents consumers from modifying entities directly.

```csharp
namespace Application.Identity.Models;

/// <summary>
/// Read-only representation of a User for public consumption.
/// </summary>
public sealed record UserDto(
    Guid Id,
    string? Username,
    string? Email,
    bool IsAnonymous,
    bool TwoFactorEnabled,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<ExternalLoginDto> ExternalLogins
);

public sealed record ExternalLoginDto(
    string Provider,
    string? DisplayName,
    string? Email
);

public sealed record UserSessionDto(
    Guid SessionId,
    Guid UserId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt
);

/// <summary>
/// Result of authentication attempt (may require 2FA).
/// </summary>
public sealed record AuthenticationResult(
    bool Succeeded,
    bool RequiresTwoFactor,
    Guid? UserId,
    UserSessionDto? Session,
    string? ErrorMessage
);

/// <summary>
/// Information needed to set up 2FA.
/// </summary>
public sealed record TwoFactorSetupInfo(
    string SharedKey,
    string AuthenticatorUri,
    string QrCodeDataUri
);
```

### Public Services (namespace Application.Identity.Interfaces)

**IUserRegistrationService (4 methods)**
```csharp
namespace Application.Identity.Interfaces;

public interface IUserRegistrationService
{
    Task<UserDto> RegisterUserAsync(UserRegistrationRequest? request, CancellationToken ct);
    Task<UserDto> RegisterExternalAsync(ExternalUserRegistrationRequest request, CancellationToken ct);
    Task UpgradeAnonymousWithPasskeyAsync(Guid userId, CancellationToken ct);
    Task DeleteUserAsync(Guid userId, CancellationToken ct);
}
```

**IAuthenticationService (4 methods)**
```csharp
namespace Application.Identity.Interfaces;

public interface IAuthenticationService
{
    Task<UserSessionDto> AuthenticateAsync(string username, string password, CancellationToken ct);
    Task<AuthenticationResult> AuthenticateWithResultAsync(string username, string password, CancellationToken ct);
    Task<UserSessionDto> CreateSessionForUserAsync(Guid userId, CancellationToken ct);
    Task<UserSessionDto> Complete2faAuthenticationAsync(Guid userId, string code, CancellationToken ct);
}
```

**IPasswordService (5 methods)**
```csharp
namespace Application.Identity.Interfaces;

public interface IPasswordService
{
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct);
    Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct);
    Task LinkPasswordAsync(Guid userId, string username, string password, string? email, CancellationToken ct);
    Task<string?> GeneratePasswordResetTokenAsync(string email, CancellationToken ct);
    Task<bool> ResetPasswordWithTokenAsync(string email, string token, string newPassword, CancellationToken ct);
}
```

**ITwoFactorService (6 methods)**
```csharp
namespace Application.Identity.Interfaces;

public interface ITwoFactorService
{
    Task<TwoFactorSetupInfo> Setup2faAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyCollection<string>> Enable2faAsync(Guid userId, string verificationCode, CancellationToken ct);
    Task Disable2faAsync(Guid userId, CancellationToken ct);
    Task<bool> Verify2faCodeAsync(Guid userId, string code, CancellationToken ct);
    Task<IReadOnlyCollection<string>> GenerateRecoveryCodesAsync(Guid userId, CancellationToken ct);
    Task<int> GetRecoveryCodeCountAsync(Guid userId, CancellationToken ct);
}
```

**IUserProfileService (9 methods)**
```csharp
namespace Application.Identity.Interfaces;

public interface IUserProfileService
{
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<UserDto?> GetByUsernameAsync(string username, CancellationToken ct);
    Task<UserDto?> GetByEmailAsync(string email, CancellationToken ct);
    Task<IReadOnlyCollection<UserDto>> ListAsync(CancellationToken ct);
    Task UpdateUserAsync(Guid userId, UserUpdateRequest request, CancellationToken ct);
    Task ChangeUsernameAsync(Guid userId, string newUsername, CancellationToken ct);
    Task ChangeEmailAsync(Guid userId, string newEmail, CancellationToken ct);
    Task LinkEmailAsync(Guid userId, string email, CancellationToken ct);
    Task UnlinkEmailAsync(Guid userId, CancellationToken ct);
}
```

**IUserRoleService (3 methods)**
```csharp
namespace Application.Identity.Interfaces;

public interface IUserRoleService
{
    Task AssignRoleAsync(Guid userId, RoleAssignmentRequest assignment, CancellationToken ct);
    Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken ct);
    Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct);
}
```

### Internal Repositories (namespace Application.Identity.Interfaces.Internal)

**IUserRepository (11 methods) - internal**
```csharp
namespace Application.Identity.Interfaces.Internal;

internal interface IUserRepository
{
    // From IUserStore
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct);
    Task<User?> FindByExternalIdentityAsync(string provider, string subject, CancellationToken ct);
    Task SaveAsync(User user, CancellationToken ct);
    Task<IReadOnlyCollection<User>> ListAsync(CancellationToken ct);
    Task<int> DeleteAbandonedAnonymousUsersAsync(DateTimeOffset cutoff, CancellationToken ct);
    
    // From IExternalLoginStore
    Task<Guid?> FindUserByLoginAsync(ExternalLoginProvider provider, string providerKey, CancellationToken ct);
    Task AddLoginAsync(Guid userId, ExternalLoginProvider provider, string providerKey, string? displayName, string? email, CancellationToken ct);
    Task<bool> RemoveLoginAsync(Guid userId, ExternalLoginProvider provider, CancellationToken ct);
    Task<IReadOnlyCollection<ExternalLoginInfo>> GetLoginsAsync(Guid userId, CancellationToken ct);
    Task<bool> HasAnyLoginAsync(Guid userId, CancellationToken ct);
}
```

**ISessionRepository (8 methods) - internal**
```csharp
namespace Application.Identity.Interfaces.Internal;

internal interface ISessionRepository
{
    Task CreateAsync(LoginSession session, CancellationToken ct);
    Task<LoginSession?> GetByIdAsync(Guid sessionId, CancellationToken ct);
    Task<IReadOnlyCollection<LoginSession>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct);
    Task UpdateAsync(LoginSession session, CancellationToken ct);
    Task<bool> RevokeAsync(Guid sessionId, CancellationToken ct);
    Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken ct);
    Task<int> RevokeAllExceptAsync(Guid userId, Guid exceptSessionId, CancellationToken ct);
    Task<int> DeleteExpiredAsync(DateTimeOffset olderThan, CancellationToken ct);
}
```

**IPasskeyRepository (10 methods) - internal**
```csharp
namespace Application.Identity.Interfaces.Internal;

internal interface IPasskeyRepository
{
    // Credentials
    Task SaveCredentialAsync(PasskeyCredential credential, CancellationToken ct);
    Task<IReadOnlyCollection<PasskeyCredential>> GetCredentialsByUserIdAsync(Guid userId, CancellationToken ct);
    Task<PasskeyCredential?> GetCredentialByIdAsync(Guid credentialId, CancellationToken ct);
    Task<PasskeyCredential?> GetCredentialByCredentialIdAsync(byte[] credentialId, CancellationToken ct);
    Task UpdateCredentialAsync(PasskeyCredential credential, CancellationToken ct);
    Task DeleteCredentialAsync(Guid credentialId, CancellationToken ct);
    
    // Challenges
    Task SaveChallengeAsync(PasskeyChallenge challenge, CancellationToken ct);
    Task<PasskeyChallenge?> GetChallengeByIdAsync(Guid challengeId, CancellationToken ct);
    Task DeleteChallengeAsync(Guid challengeId, CancellationToken ct);
    Task DeleteExpiredChallengesAsync(CancellationToken ct);
}
```

### Authorization (namespace Application.Authorization.Interfaces)

**ITokenService (3 methods) - public, new**
```csharp
namespace Application.Authorization.Interfaces;

public interface ITokenService
{
    Task<string> GenerateAccessTokenAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct);
    Task<string> GenerateRefreshTokenAsync(Guid sessionId, CancellationToken ct);
    Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken ct);
}

public sealed record TokenValidationResult(
    bool IsValid,
    Guid? UserId,
    Guid? SessionId,
    IReadOnlyCollection<string>? Roles,
    string? ErrorMessage
);
```

**IRoleRepository - internal (namespace Application.Authorization.Interfaces.Internal)**
```csharp
namespace Application.Authorization.Interfaces.Internal;

internal interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Role?> GetByCodeAsync(string code, CancellationToken ct);
    Task<IReadOnlyCollection<Role>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);
    Task<IReadOnlyCollection<Role>> ListAsync(CancellationToken ct);
    Task SaveAsync(Role role, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
```

---

## Implementation Phases

### Phase 1: Folder & Namespace Reorganization
- [ ] Create `Application/Identity/Interfaces/` folder
- [ ] Create `Application/Identity/Interfaces/Internal/` folder
- [ ] Create `Application/Authorization/Interfaces/` folder
- [ ] Create `Application/Authorization/Interfaces/Internal/` folder
- [ ] Add `InternalsVisibleTo` in `Application/AssemblyInfo.cs`
- [ ] Move existing interfaces to new locations with updated namespaces

### Phase 2: Interface Refactoring
- [ ] Create 6 new service interfaces (split from IIdentityService)
- [ ] Create 3 merged repository interfaces (internal)
- [ ] Create ITokenService abstraction (public)
- [ ] Mark all repository interfaces as `internal`
- [ ] Move JWT interfaces to Infrastructure.Jwt
- [ ] Delete IRoleLookup, IPermissionServiceFactory

### Phase 3: Service Implementation
- [ ] Implement 6 new services (delegate to existing IdentityService initially)
- [ ] Implement merged repositories
- [ ] Update DI registrations
- [ ] Keep IIdentityService as deprecated facade

### Phase 4: ASP.NET Identity Abstraction
- [ ] Create IPasswordHasher interface
- [ ] Create IAuthenticatorService interface
- [ ] Create Infrastructure.Identity project
- [ ] Move ASP.NET Identity usage to infrastructure

### Phase 5: Domain Enrichment
- [ ] Add value objects: Username, Email, Password
- [ ] Add domain events
- [ ] Move business rules to User entity
- [ ] Create domain services

### Phase 6: Cleanup
- [ ] Remove IIdentityService
- [ ] Remove granular store interfaces
- [ ] Update all consumers to new services

---

## Summary

| Category | Before | After |
|----------|--------|-------|
| Identity Public Services | 4 (1 god + 3 focused) | 9 (all focused) |
| Identity Internal Repos | 5 (public, granular) | 3 (internal, per aggregate) |
| Authorization Public Services | 3 | 4 (+ITokenService) |
| Authorization Internal Repos | 2 (public, duplicate) | 1 (internal) |
| **Total Public Interfaces** | **9** | **13** |
| **Total Internal Interfaces** | **0** | **4** |
| Moved to Infrastructure | - | 3 |
| Deleted | - | 2 |
