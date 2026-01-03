# TODO: DDD and Clean Architecture Improvements

## Current Problems

1. **God Service**: `IIdentityService` has 25+ methods covering registration, authentication, 2FA, sessions, password management
2. **Granular Stores**: 5 separate store interfaces (`IUserStore`, `ISessionStore`, `IPasskeyCredentialStore`, `IPasskeyChallengeStore`, `IExternalLoginStore`) - too fine-grained
3. **Mixed Concerns**: `IIdentityService` handles both application logic AND data access coordination
4. **Infrastructure Interfaces in Application**: Public infrastructure interfaces (factories, stores) live in Application - violates ignorance
5. **ASP.NET Identity Coupling**: `IdentityService` directly uses ASP.NET Identity's `UserManager`, `SignInManager` in Application layer
6. **Duplicate Interfaces**: `IRoleLookup` duplicates `IRoleRepository` functionality
7. **Domain Entities Exposed**: Public services return mutable domain entities instead of DTOs

---

## Target Folder Structure

```
Application/
├── Identity/
│   ├── Interfaces/
│   │   ├── IUserRegistrationService.cs      (public)
│   │   ├── IAuthenticationService.cs        (public)
│   │   ├── IPasswordService.cs              (public)
│   │   ├── ITwoFactorService.cs             (public)
│   │   ├── IUserProfileService.cs           (public)
│   │   ├── IUserRoleService.cs              (public)
│   │   ├── IPasskeyService.cs               (public)
│   │   ├── IOAuthService.cs                 (public)
│   │   ├── IAnonymousUserCleanupService.cs  (public)
│   │   └── Infrastructure/
│   │       ├── IUserRepository.cs           (internal)
│   │       ├── ISessionRepository.cs        (internal)
│   │       └── IPasskeyRepository.cs        (internal)
│   └── Models/
│       ├── UserDto.cs
│       ├── ExternalLoginDto.cs
│       ├── UserSessionDto.cs
│       ├── AuthenticationResult.cs
│       └── TwoFactorSetupInfo.cs
│
├── Authorization/
│   ├── Interfaces/
│   │   ├── IRoleService.cs                  (public)
│   │   ├── IPermissionService.cs            (public)
│   │   ├── IUserRoleResolver.cs             (public)
│   │   ├── ITokenService.cs                 (public)
│   │   ├── Infrastructure/
│   │   │   └── IRoleRepository.cs           (internal)
│   │   └── Application/
│   │       └── IAuthorizedHttpClientFactory.cs (internal)
│   └── Models/
│       └── TokenValidationResult.cs
```

### Folder Naming Convention

- `Interfaces/` - Public interfaces for external consumers
- `Interfaces/Infrastructure/` - Internal interfaces implemented by Infrastructure projects
- `Interfaces/Application/` - Internal interfaces implemented by Application layer services (cross-service utilities)
- `Models/` - DTOs and result types

### Assembly Access Control

```csharp
// In Application/AssemblyInfo.cs
[assembly: InternalsVisibleTo("Infrastructure.EFCore.Identity")]
[assembly: InternalsVisibleTo("Application.Tests")]
```

### Consumer Usage

```csharp
// ✅ Consumers can inject public services
using Application.Identity.Interfaces;

public class OrderController(
    IUserProfileService userProfile,      // ✅ public - allowed
    IAuthenticationService auth           // ✅ public - allowed
) { }

// ❌ Consumers CANNOT inject infrastructure interfaces
using Application.Identity.Interfaces.Infrastructure;  // Namespace exists but...

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

### Application/Identity/Interfaces/Infrastructure/ (internal)

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

### Application/Authorization/Interfaces/Infrastructure/ (internal)

| Interface | Action |
|-----------|--------|
| `IRoleRepository` | Keep (absorb IRoleLookup) |

### Application/Authorization/Interfaces/Application/ (internal)

| Interface | Action |
|-----------|--------|
| `IAuthorizedHttpClientFactory` | Internal utility for Application services |

### Move to Infrastructure (implementation only)

| Interface | Implemented In | Reason |
|-----------|----------------|--------|
| `IJwtTokenService` | `Infrastructure.EFCore.Identity` | JWT implementation detail |
| `IJwtTokenServiceFactory` | `Infrastructure.EFCore.Identity` | JWT implementation detail |

### Delete

| Interface | Reason |
|-----------|--------|
| `IRoleLookup` | Duplicate of IRoleRepository |
| `IPermissionServiceFactory` | Infra wiring detail |

---

## Detailed Interface Definitions

### Identity DTOs (namespace Application.Identity.Models)

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

### Authorization DTOs (namespace Application.Authorization.Models)

```csharp
namespace Application.Authorization.Models;

/// <summary>
/// Result of token validation.
/// </summary>
public sealed record TokenValidationResult(
    bool IsValid,
    Guid? UserId,
    Guid? SessionId,
    IReadOnlyCollection<string>? Roles,
    string? ErrorMessage
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

### Identity Infrastructure Interfaces (namespace Application.Identity.Interfaces.Infrastructure)

**IUserRepository (11 methods) - internal**
```csharp
namespace Application.Identity.Interfaces.Infrastructure;

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
namespace Application.Identity.Interfaces.Infrastructure;

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
namespace Application.Identity.Interfaces.Infrastructure;

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

### Authorization Public Interfaces (namespace Application.Authorization.Interfaces)

**ITokenService (3 methods) - public, new**
```csharp
namespace Application.Authorization.Interfaces;

public interface ITokenService
{
    Task<string> GenerateAccessTokenAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct);
    Task<string> GenerateRefreshTokenAsync(Guid sessionId, CancellationToken ct);
    Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken ct);
}
```

### Authorization Infrastructure Interfaces (namespace Application.Authorization.Interfaces.Infrastructure)

**IRoleRepository - internal**
```csharp
namespace Application.Authorization.Interfaces.Infrastructure;

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

### Authorization Application Interfaces (namespace Application.Authorization.Interfaces.Application)

**IAuthorizedHttpClientFactory - internal**
```csharp
namespace Application.Authorization.Interfaces.Application;

/// <summary>
/// Internal utility for Application services to create HTTP clients with authorization.
/// Implemented by Application layer services, not Infrastructure.
/// </summary>
internal interface IAuthorizedHttpClientFactory
{
    HttpClient CreateClient(string name);
    HttpClient CreateAuthorizedClient(Guid userId, IReadOnlyCollection<string> roles);
}
```

---

## Implementation Phases

### Phase 1: Folder & Namespace Reorganization
- [ ] Create `Application/Identity/Interfaces/Infrastructure/` folder
- [ ] Create `Application/Authorization/Interfaces/Infrastructure/` folder
- [ ] Create `Application/Authorization/Interfaces/Application/` folder
- [ ] Create `Application/Identity/Models/` folder
- [ ] Create `Application/Authorization/Models/` folder
- [ ] Add `InternalsVisibleTo` in `Application/AssemblyInfo.cs`
- [ ] Move existing interfaces to new locations with updated namespaces

### Phase 2: Interface Refactoring
- [ ] Create 6 new service interfaces (split from IIdentityService)
- [ ] Create 3 merged repository interfaces (internal)
- [ ] Create ITokenService abstraction (public)
- [ ] Move IAuthorizedHttpClientFactory to Authorization/Interfaces/Application/
- [ ] Mark all repository interfaces as `internal`
- [ ] Move JWT interfaces to `Infrastructure.EFCore.Identity`
- [ ] Delete IRoleLookup, IPermissionServiceFactory

### Phase 3: DTO Creation
- [ ] Create Identity DTOs in Application.Identity.Models
- [ ] Create Authorization DTOs in Application.Authorization.Models
- [ ] Update service interfaces to return DTOs instead of entities

### Phase 4: Service Implementation
- [ ] Implement 6 new services in `Infrastructure.EFCore.Identity` (delegate to existing IdentityService initially)
- [ ] Implement merged repositories in `Infrastructure.EFCore.Identity`
- [ ] Update DI registrations
- [ ] Keep IIdentityService as deprecated facade

### Phase 5: ASP.NET Identity Abstraction
- [ ] Create IPasswordHasher interface in Application
- [ ] Create IAuthenticatorService interface in Application
- [ ] Implement abstractions in `Infrastructure.EFCore.Identity`
- [ ] Move ASP.NET Identity usage fully to `Infrastructure.EFCore.Identity`

### Phase 6: Domain Enrichment
- [ ] Add value objects: Username, Email, Password
- [ ] Add domain events
- [ ] Move business rules to User entity
- [ ] Create domain services

### Phase 7: Cleanup
- [ ] Remove IIdentityService
- [ ] Remove granular store interfaces
- [ ] Update all consumers to new services

---

## Summary

| Category | Before | After |
|----------|--------|-------|
| Identity Public Services | 4 (1 god + 3 focused) | 9 (all focused) |
| Identity Infrastructure Interfaces | 5 (public, granular) | 3 (internal repos) |
| Authorization Public Services | 3 | 4 (+ITokenService) |
| Authorization Infrastructure Interfaces | 2 (public, duplicate) | 1 (internal) |
| Authorization Application Interfaces | 0 | 1 (IAuthorizedHttpClientFactory) |
| **Total Public Interfaces** | **9** | **13** |
| **Total Internal Interfaces** | **0** | **5** (3 identity repos + 1 auth repo + 1 auth utility) |
| JWT interfaces | In Application | Moved to `Infrastructure.EFCore.Identity` |
| Deleted | - | 2 (IRoleLookup, IPermissionServiceFactory) |
| **New Projects** | - | **0** (use existing) |
