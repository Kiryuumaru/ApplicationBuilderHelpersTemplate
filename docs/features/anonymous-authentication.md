# Anonymous Authentication (Guest Mode)

**Status:** ✅ Complete (December 26, 2025)

## Overview

Zero-friction onboarding allowing users to start playing immediately without signup. Users can later upgrade their anonymous account by linking credentials (password, OAuth, passkey).

## User Flow

```
User visits → POST /auth/register (empty body) → Anonymous account created
                                                        ↓
                                    User interacts with the application
                                                        ↓
                               Prompt: "Save progress?" → POST /auth/link/password
                                                        ↓
                                        Same userId, all data preserved
```

## Domain Model

### User Entity

Anonymous users are represented using the standard `User` entity with specific characteristics:

| Property | Anonymous User | Regular User |
|----------|---------------|--------------|
| `Id` | Generated UUID | Generated UUID |
| `UserName` | `null` | Required (unique) |
| `NormalizedUserName` | `null` | Required (unique, uppercase) |
| `Email` | Optional | Optional |
| `PasswordHash` | `null` | Required |
| `IsAnonymous` | `true` | `false` |
| `LinkedAt` | `null` | Set when upgraded from anonymous |

### Key Design Decisions

1. **Nullable UserName:** Anonymous users have no username. The `UserName` and `NormalizedUserName` properties are nullable (`string?`) to properly represent this state without placeholder values.

2. **IsAnonymous Flag:** Boolean flag indicating account type. Once `false`, cannot be reverted.

3. **LinkedAt Timestamp:** Records when an anonymous account was upgraded to a full account, useful for analytics and audit.

4. **No Merging:** Anonymous accounts cannot be merged with existing accounts. If a user tries to link credentials already associated with another account, the operation is rejected with 409 Conflict.

## Anonymous User Creation

The `User.RegisterAnonymous()` factory method creates anonymous users:

```csharp
public static User RegisterAnonymous()
{
    var now = DateTimeOffset.UtcNow;
    return new User
    {
        Id = Guid.NewGuid().ToString(),
        UserName = null,           // No username
        NormalizedUserName = null, // No normalized username
        IsAnonymous = true,        // Marked as anonymous
        LockoutEnabled = false,
        RevId = Guid.NewGuid().ToString(),
        Created = now,
        LastModified = now,
    };
}
```

## Register Endpoint Enhancement

The existing `POST /api/v1/auth/register` endpoint now supports anonymous registration:

### Request Examples

**Anonymous Registration (Empty Body):**
```json
POST /api/v1/auth/register
{}
```

**Response (201 Created):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1...",
  "refreshToken": "eyJhbGciOiJIUzI1...",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "username": null,
    "email": null,
    "isAnonymous": true,
    "roles": ["user"],
    "permissions": ["users._read", "users._write", ...]
  }
}
```

### Registration Behavior Matrix

| Provided | Result |
|----------|--------|
| Nothing | Anonymous account (`isAnonymous: true`, `username: null`) |
| Email only | Anonymous with email linked |
| Username + Password | Full account (`isAnonymous: false`) |
| All fields | Full account with email |

## Custom Identity Validation

ASP.NET Core Identity's default `UserValidator` rejects null/empty usernames. A custom `AnonymousUserValidator` is registered to allow null usernames for anonymous users:

```csharp
public class AnonymousUserValidator : IUserValidator<User>
{
    private readonly UserValidator<User> _defaultValidator;

    public AnonymousUserValidator(IdentityErrorDescriber? errors = null)
    {
        _defaultValidator = new UserValidator<User>(errors);
    }

    public async Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user)
    {
        // Allow null/empty username for anonymous users
        if (user.IsAnonymous && string.IsNullOrEmpty(user.UserName))
        {
            return IdentityResult.Success;
        }

        // For non-anonymous users, use default validation
        return await _defaultValidator.ValidateAsync(manager, user);
    }
}
```

## Database Schema

The EF Core configuration allows nullable `UserName` and `NormalizedUserName`:

```csharp
entity.Property(u => u.UserName).HasMaxLength(256);  // No .IsRequired()
entity.Property(u => u.NormalizedUserName).HasMaxLength(256);  // No .IsRequired()
```

**Users Table Schema:**
```sql
CREATE TABLE "Users" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "UserName" TEXT NULL,              -- Nullable for anonymous
    "NormalizedUserName" TEXT NULL,    -- Nullable for anonymous
    "Email" TEXT NULL,
    "IsAnonymous" INTEGER NOT NULL,    -- Boolean flag
    "LinkedAt" TEXT NULL,              -- When upgraded from anonymous
    ...
);
```

## JWT Token Generation

The `PermissionService` accepts nullable usernames for token generation:

```csharp
public async Task<string> GenerateTokenWithScopeAsync(
    string userId,
    string? username,  // Nullable for anonymous users
    IEnumerable<ScopeDirective> scopeDirectives,
    ...)
{
    // Uses empty string for token if username is null
    return await jwtTokenService.GenerateToken(
        userId: userId,
        username: username ?? string.Empty,
        scopes: normalizedScopes,
        ...);
}
```

## Files Modified

| File | Change |
|------|--------|
| `Domain/Identity/Entities/User.cs` | Made `UserName`, `NormalizedUserName` nullable; added `RegisterAnonymous()` |
| `Application/Identity/Models/UserSession.cs` | Made `Username` nullable |
| `Application/Identity/Models/UserInfo.cs` | Made `Username` nullable |
| `Application/Identity/Services/AnonymousUserValidator.cs` | New - custom validator |
| `Application/Identity/Extensions/IdentityServiceCollectionExtensions.cs` | Register custom validator |
| `Application/Authorization/Interfaces/IPermissionService.cs` | Made `username` parameter nullable |
| `Application/Authorization/Services/PermissionService.cs` | Handle nullable username |
| `Infrastructure.EFCore.Identity/Configurations/IdentityEntityConfiguration.cs` | Removed `IsRequired()` |
| `Presentation.WebApi/Models/Responses/UserResponse.cs` | Made `Username` nullable |
| `Presentation.WebApi/Controllers/V1/AuthController.cs` | Handle nullable username |

## Test Coverage

### Register Tests (Anonymous Support)

| Test | Description |
|------|-------------|
| `Register_WithEmptyBody_CreatesAnonymousUser` | Empty body creates anonymous user with null username |
| `Register_WithNoFields_ReturnsTokens` | Anonymous gets valid JWT tokens |
| `Register_WithNoFields_IsAnonymousTrue` | Response shows isAnonymous: true |
| `Register_WithEmailOnly_CreatesAnonymousWithEmail` | Email only = still anonymous |
| `Register_WithUsernameAndPassword_CreatesFullAccount` | Full account, isAnonymous: false |
| `Register_WithAllFields_CreatesFullAccountWithEmail` | Complete registration |

### Login Tests (Username or Email)

| Test | Description |
|------|-------------|
| `Login_WithUsername_Succeeds` | Login using username + password |
| `Login_WithEmail_Succeeds` | Login using email + password |
| `Login_WithUsername_CaseInsensitive` | Username login ignores case |
| `Login_WithEmail_CaseInsensitive` | Email login ignores case |
| `Login_Anonymous_Returns401` | Anonymous accounts can't login (no password) |

### Identity Linking Tests

| Test | Description |
|------|-------------|
| `LinkPassword_ToAnonymous_BecomesNonAnonymous` | Linking password upgrades account |
| `LinkPassword_WithExistingUsername_Returns409` | Duplicate username rejected |
| `LinkPassword_PreservesExistingData` | User data preserved after linking |
| `LinkEmail_ToAnonymous_StillAnonymous` | Email alone doesn't upgrade |
| `LinkEmail_AlreadyLinked_Returns409` | Duplicate email rejected |
| `LinkPasskey_ToAnonymous_BecomesNonAnonymous` | Passkey linking upgrades account |

### Identity Management Tests

| Test | Description |
|------|-------------|
| `GetIdentities_ReturnsAllLinkedProviders` | Shows email, password, oauth, passkeys |
| `GetIdentities_ShowsCanUnlinkCorrectly` | canUnlink reflects safety rules |
| `ChangeUsername_WithValidName_Succeeds` | Username change works |
| `ChangeUsername_ToExisting_Returns409` | Duplicate username rejected |
| `ChangeEmail_WithValidEmail_Succeeds` | Email change works |
| `UnlinkEmail_WhenHasOtherAuth_Succeeds` | Can remove email |

### Unlink Safety Tests

| Test | Description |
|------|-------------|
| `UnlinkOAuth_WhenLastAuthMethod_Returns409` | Cannot remove last auth |
| `UnlinkPasskey_WhenLastAuthMethod_Returns409` | Cannot remove last auth |
| `UnlinkPasskey_WhenHasOtherPasskey_Succeeds` | Can remove one of many |

### Security Tests

| Test | Description |
|------|-------------|
| `LinkPassword_RequiresAuth` | Must be authenticated |
| `GetIdentities_RequiresAuth` | Must be authenticated |
| `Anonymous_HasUserRole` | Gets default user role |

## Completed Identity Linking Endpoints

| Endpoint | Description | Status |
|----------|-------------|--------|
| `POST /api/v1/auth/link/email` | Link email (for recovery/notifications) | ✅ |
| `POST /api/v1/auth/link/password` | Link username + password (becomes non-anonymous) | ✅ |
| `POST /api/v1/auth/link/passkey` | Link WebAuthn passkey (becomes non-anonymous) | ✅ |
| `GET /api/v1/auth/identities` | Get all linked identity providers | ✅ |
| `PUT /api/v1/auth/identities/username` | Change username | ✅ |
| `PUT /api/v1/auth/identities/email` | Change email | ✅ |
| `DELETE /api/v1/auth/identities/email` | Unlink email | ✅ |
| `DELETE /api/v1/auth/link/{provider}` | Unlink OAuth provider | ✅ |

## Background Cleanup

The `AnonymousUserCleanupWorker` background service automatically deletes abandoned anonymous accounts:
- **Criteria:** 30+ days inactive AND no trades
- **Frequency:** Runs daily

## Future Plans

The following OAuth linking endpoints are planned for future implementation:

- `POST /api/v1/auth/link/google` - Link Google OAuth
- `POST /api/v1/auth/link/github` - Link GitHub OAuth

See [roadmap-future.md](../roadmap-future.md) for the complete future roadmap.

## Security Considerations

1. **Anonymous accounts have limited permissions:** Cannot access admin endpoints
2. **Session-based authentication:** Anonymous users get full JWT tokens with sessions
3. **No account merging:** Prevents security issues from combining accounts
4. **Unlink safety:** Cannot unlink last auth method (future implementation)
