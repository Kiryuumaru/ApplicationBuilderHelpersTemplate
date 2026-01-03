# What I Did Wrong During Refactoring - Found By Dev

This document lists all the critical mistakes I made during the DDD/Clean Architecture refactoring that broke existing functionality.

---

## 1. IPermissionService Has TokenValidationParameters (Doesn't Belong There)

**Location:** `src/Application/Authorization/Interfaces/IPermissionService.cs:168`

**The Problem:**
```csharp
Task<Microsoft.IdentityModel.Tokens.TokenValidationParameters> GetTokenValidationParametersAsync(CancellationToken cancellationToken = default);
```

**Why It's Wrong:**
- `TokenValidationParameters` is a JWT/security infrastructure concern
- It should NOT be in `IPermissionService` which is an Application layer interface
- This violates clean architecture - Application layer should not know about JWT implementation details
- This should be in a separate `ITokenService` or `ISecurityService` interface in Infrastructure

**Files Affected:**
- `src/Application/Authorization/Interfaces/IPermissionService.cs`
- `src/Application/Authorization/Services/PermissionService.cs`
- `src/Presentation.WebApi/ConfigureOptions/ConfigureJwtBearerOptions.cs`

---

## 2. LinkPassword Validation Was Removed

**Location:** `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Identity.cs`

**The Removed Code:**
```csharp
// Check if password is already linked
if (!string.IsNullOrEmpty(user.PasswordHash))
{
    return BadRequest(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Password already linked",
        Detail = "This account already has a password. Use change-password to update it."
    });
}
```

**Why It's Wrong:**
- Users should NOT be able to re-link a password if they already have one
- The correct flow is: if user has password → use `change-password` endpoint
- This validation was a critical business rule that prevented security issues
- Now users can overwrite their password without verifying the old one

---

## 3. Permissions Array Returned as Empty in All Auth Responses

**Locations:**
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Login.cs:73`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Login.cs:177`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Login.cs:239`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Passkey.cs:149`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.OAuth.cs:209`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.TwoFactor.cs:176`

**What Was Changed:**
FROM:
```csharp
Permissions = userSession.PermissionIdentifiers
```

TO:
```csharp
Permissions = []
```

**Why It's Wrong:**
- The `UserSession` domain model has `PermissionIdentifiers` property that was populated
- Now ALL auth responses return empty permissions array
- Clients depending on the permissions in the login response will break
- The `UserSession` model properly calculated effective permissions from roles + direct grants

---

## 4. Direct Permission Grants Ignored

**Location:** `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Identity.cs` (LinkPassword endpoint)

**What Was Changed:**
FROM:
```csharp
Permissions = updatedUser.PermissionGrants.Select(g => g.Identifier).ToArray()
```

TO:
```csharp
Permissions = permissions.ToArray()  // only role-based permissions
```

**Why It's Wrong:**
- Users can have DIRECT permission grants (via `User.PermissionGrants`)
- Users can also have role-based permissions (via `User.RoleAssignments`)
- Both should be combined in the response
- Now only role-based permissions are returned, direct grants are ignored
- The domain model `User` has both `PermissionGrants` and `RoleAssignments` for a reason

---

## 5. Critical Security Feature Removed - Refresh Token Theft Detection

**Location:** `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Token.cs` (Refresh endpoint)

**The Removed Code:**
```csharp
// Verify the refresh token hash matches (detect token theft)
var tokenHash = HashToken(request.RefreshToken);
if (!string.Equals(loginSession.RefreshTokenHash, tokenHash, StringComparison.Ordinal))
{
    // Token theft detected! Revoke the entire session
    await sessionStore.RevokeAsync(sessionId, cancellationToken);
    return Unauthorized(new ProblemDetails
    {
        Status = StatusCodes.Status401Unauthorized,
        Title = "Session revoked",
        Detail = "This session has been revoked for security reasons."
    });
}
```

**Current Implementation:**
The refresh endpoint now validates via `sessionService.ValidateSessionAsync` but...

**What Might Be Wrong:**
1. The `ValidateSessionAsync` may not be doing the same token hash comparison
2. The automatic session revocation on theft detection may not be happening
3. Need to verify `SessionService.ValidateSessionAsync` properly:
   - Compares token hash
   - Revokes session on mismatch (theft detection)
   - Returns null after revocation

**Why It Matters:**
- Token theft detection is a CRITICAL security feature
- If attacker steals old refresh token and legitimate user refreshes first, the attacker's refresh should:
  1. Detect hash mismatch (old token vs new hash in DB)
  2. Revoke the ENTIRE session immediately
  3. Force both users to re-authenticate
- This protects against refresh token replay attacks

---

## 6. Double Session Creation Bug

**Locations:**
- `src/Application/Identity/Services/AuthenticationService.cs` - creates `LoginSession`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Helpers.cs:CreateSessionAndTokensAsync` - creates ANOTHER session

**The Flow:**
1. `Login` → `authenticationService.AuthenticateWithResultAsync` creates a `LoginSession` in DB
2. Then calls `CreateSessionAndTokensAsync` which creates ANOTHER `LoginSession` in DB
3. Result: TWO sessions for one login

**Evidence from Test Failures:**
```
ListSessions_AfterRegister_ReturnsOneSession:
  Expected: 1 session
  Actual: 2 sessions

ListSessions_AfterMultipleLogins_ReturnsMultipleSessions:
  Expected: 2
  Actual: 4

EachLogin_CreatesNewSession:
  Expected: 3
  Actual: 6
```

**Why It's Wrong:**
- Each login/register should create exactly ONE session
- We're creating TWO sessions per authentication
- This doubles the session count and wastes storage
- It also breaks session management/revocation logic

---

## 7. User.RoleAssignments Not Being Loaded from Database

**Location:** `src/Infrastructure.EFCore.Identity/Services/EFCoreUserRepository.cs`

**The Problem:**
- `User.RoleAssignments` is marked as `Ignored` in EF Core configuration
- Role assignments are stored in separate `UserRoleAssignments` table
- BUT the repository methods don't load them:
  - `FindByIdAsync` - doesn't load role assignments
  - `FindByUsernameAsync` - doesn't load role assignments
  - `FindByEmailAsync` - doesn't load role assignments
  - `SaveAsync` - doesn't sync role assignments

**Fix Attempted:**
I added helper methods `HydrateRoleAssignmentsAsync` and `SyncRoleAssignmentsAsync` but the tests still show empty role collections. Need to verify:
1. The query is correct
2. The table name matches
3. The User.AssignRole method is being called properly

---

## 8. Static Roles Not Returned by Repository (Fixed)

**Location:** `src/Infrastructure.EFCore.Identity/Services/EFCoreRoleRepository.cs`

**The Problem:**
- ADMIN and USER roles are defined as static constants in `Domain.Authorization.Constants.Roles`
- They are NOT stored in the database
- The `EFCoreRoleRepository` was only querying the database, not checking static roles

**Status:** FIXED - Added checks for static roles before querying database

---

## 9. User.PermissionGrants Not Being Persisted

**Location:** Similar to RoleAssignments issue

**The Problem:**
- `User.PermissionGrants` is also marked as `Ignored` in EF Core configuration
- This means direct permission grants assigned to users via `User.GrantPermission()` are never saved
- Need a separate table `UserPermissionGrants` and corresponding repository logic

---

## Summary of Test Failures Caused

| Test Category | Failures | Root Cause |
|--------------|----------|------------|
| Session count tests | 6 | Double session creation |
| Permission check tests | 7 | Empty permissions returned, grants not loaded |
| Role assignment tests | 5 | RoleAssignments not loaded from DB |
| Security tests | 4 | Token theft detection may be broken |
| Authorization (403) tests | 6 | Permissions empty, all requests allowed |

---

## What Should Have Been Done

1. **Split and move code** - NOT modify business logic
2. **Keep DTOs identical** - response formats should not change
3. **Keep security features** - especially token theft detection
4. **Keep validations** - business rules like "password already linked"
5. **Keep permission flow** - direct grants + role-based should combine
6. **Test after each change** - run tests to catch regressions immediately
7. **Don't create double sessions** - reuse the session from AuthenticationService

---

## Priority Fixes Needed

1. **HIGH** - Fix double session creation (affects all auth endpoints)
2. **HIGH** - Restore token theft detection in refresh endpoint
3. **HIGH** - Fix RoleAssignments not loading from DB
4. **HIGH** - Return actual permissions in auth responses (not empty array)
5. **MEDIUM** - Restore "password already linked" validation
6. **MEDIUM** - Fix PermissionGrants persistence
7. **LOW** - Move TokenValidationParameters to proper interface
