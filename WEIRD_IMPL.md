# Weird Implementation Findings

This document contains findings from deep code analysis (3 passes), merged and ranked by severity from **highest to lowest**.

---

## Fix Status Legend

- ✅ **WILL FIX** - Approved for fixing
- ⏭️ **SKIP** - Acceptable as-is, no fix needed
- 📝 **NOTES** - Special instructions for the fix

---

## 🔴 CRITICAL: Security Vulnerabilities

### 1. Password Reset Does Not Verify Token ✅ WILL FIX

**Severity:** 🔴 **CRITICAL SECURITY**

**Files:**
- [PasswordService.cs#L145](src/Application/Identity/Services/PasswordService.cs#L145)
- [PasswordService.cs#L157](src/Application/Identity/Services/PasswordService.cs#L157)

**Problem:** The password reset flow generates tokens but **never stores or verifies them**:

```csharp
public async Task<string?> GeneratePasswordResetTokenAsync(...)
{
    var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    // TODO: Store the token with expiration for later verification
    return token;  // Token is returned but never stored!
}

public async Task<bool> ResetPasswordWithTokenAsync(...)
{
    // TODO: Verify the token
    // For now, just reset the password  <-- NO VERIFICATION!
    user.SetPasswordHash(...);
}
```

**Risk:** **Anyone can reset any user's password without a valid token.** This completely bypasses password reset security.

**Fix:** Implement proper token storage/verification OR remove the endpoint until implemented.

---

### 2. 2FA Code Never Verified ✅ WILL FIX

**Severity:** 🔴 **CRITICAL SECURITY**

**File:** [AuthenticationService.cs#L129-L138](src/Application/Identity/Services/AuthenticationService.cs#L129-L138)

**Problem:** The `Complete2faAuthenticationAsync` method accepts a `code` parameter but **completely ignores it**:

```csharp
public async Task<UserSessionDto> Complete2faAuthenticationAsync(Guid userId, string code, CancellationToken cancellationToken)
{
    var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
        ?? throw new EntityNotFoundException("User", userId.ToString());

    // TODO: Verify 2FA code using ITwoFactorService  <-- NEVER DONE!

    return await CreateSessionForUserInternalAsync(user, cancellationToken).ConfigureAwait(false);
}
```

**Risk:** **Any code (including empty string) bypasses 2FA entirely.** Two-factor authentication provides zero protection.

**Fix:** Call `ITwoFactorService.Verify2faCodeAsync(userId, code, cancellationToken)` and throw if invalid:
```csharp
if (!await _twoFactorService.Verify2faCodeAsync(userId, code, cancellationToken))
    throw new AuthenticationException("Invalid 2FA code.");
```

---

### 3. Legacy Token Backward Compatibility Grants Full Admin ✅ WILL FIX

**Severity:** 🔴 **CRITICAL SECURITY**

📝 **Note:** Remove ALL backward compatibility support throughout the codebase. This is a template project.

**File:** [PermissionService.cs#L746-L750](src/Application/Authorization/Services/PermissionService.cs#L746-L750)

```csharp
private static bool IsLegacyRbacVersion(string? version)
{
    // null (missing) or "1" = legacy token → grant full admin access
    return string.IsNullOrEmpty(version) || string.Equals(version, "1", StringComparison.Ordinal);
}
```

**Risk:** **Any token without RBAC version claim gets unlimited admin access.** An attacker who obtains any legacy token bypasses all permission checks.

**Fix:** Remove `IsLegacyRbacVersion()` entirely. Remove all backward compatibility code.

---

## 🔴 CRITICAL: Bugs & Runtime Failures

### 4. PasskeyService Using Static Dictionary (Thread-Unsafe) ✅ WILL FIX

**Severity:** 🔴 **CRITICAL BUG**

**File:** [PasskeyService.cs#L25](src/Infrastructure.Passkeys/PasskeyService.cs#L25)

```csharp
private static readonly Dictionary<Guid, string> _pendingCredentialNames = new();
```

**Risks:**
1. **Race conditions** - Concurrent registrations corrupt state
2. **Memory leak** - Failed cleanups accumulate entries
3. **Breaks in production** - Multi-server deployments share nothing

**Fix:** Store `credentialName` in the `PasskeyChallenge` entity or use distributed cache.

---

### 5. User.SetEmail Logic Bug ✅ WILL FIX

**Severity:** 🔴 **CRITICAL BUG**

**File:** [User.cs#L590-L614](src/Domain/Identity/Models/User.cs#L590-L614)

```csharp
public void SetEmail(string? email, bool markVerified)
{
    SetEmail(email);
    if (markVerified)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
             EmailConfirmed = true;  // <-- Sets confirmed even when email is null!
        }
        else
        {
            EmailConfirmed = true;
        }
    }
    // ...
}
```

**Problem:** Sets `EmailConfirmed = true` even when email is null/empty. The inline comment questions the logic, then implements it anyway ("doctored by tests").

**Fix:** 
```csharp
EmailConfirmed = markVerified && !string.IsNullOrWhiteSpace(email);
```

---

### 6. macOS NotImplementedException ✅ WILL FIX

**Severity:** 🔴 **CRITICAL BUG**

**File:** [CliHelpers.cs#L94](src/Application/Common/Extensions/CliHelpers.cs#L94)

```csharp
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    osCli = Cli.Wrap("/bin/bash")...
}
else
{
    throw new NotImplementedException();  // macOS crashes here!
}
```

**Fix:** Combine Linux and macOS:
```csharp
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
```

---

### 7. async void Method ✅ WILL FIX

**Severity:** 🔴 **CRITICAL BUG RISK**

**File:** [RoutineExecutor.cs#L5](src/Application/Common/Extensions/RoutineExecutor.cs#L5)

```csharp
public static async void Execute(...)
```

**Problem:** `async void` methods can crash the application if exceptions escape.

**Fix:** Return `Task` instead.

---

## 🟠 HIGH: Architecture Violations

### 8. Presentation Layer Contains Business Logic ✅ WILL FIX

**Severity:** 🟠 **HIGH**

**File:** [AuthController.Token.cs#L106](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Token.cs#L106)

**Problem:** `HashToken()` method in controller. Crypto operations belong in Application layer.

**Fix:** Move to shared utility in Application layer.

---

### 9. PasskeyService Creates Sessions Directly + Returns Empty Tokens ✅ WILL FIX

**Severity:** 🟠 **HIGH**

**Files:**
- [PasskeyService.cs#L259](src/Infrastructure.Passkeys/PasskeyService.cs#L259)
- [PasskeyService.cs#L259-L280](src/Infrastructure.Passkeys/PasskeyService.cs#L259-L280)

**Problems:**
1. Infrastructure layer creates domain sessions with hardcoded 24h expiration
2. Returns `UserSessionDto` with empty tokens expecting caller to fill them
3. Uses different expiration than other flows (24h vs 7 days)

**Fix:** Delegate to `IAuthenticationService` or `IUserTokenService` for proper token generation.

---

### 10. Two Parallel Token Generation Flows ✅ WILL FIX

**Severity:** 🟠 **HIGH**

📝 **Note:** `IAuthenticationService` should use `IUserTokenService` instead of generating tokens directly.

**File:** [AuthenticationService.cs](src/Application/Identity/Services/AuthenticationService.cs)

**Problem:** Both `AuthenticationService` and `UserTokenService` generate tokens independently, creating two parallel paths.

**Fix:** Remove token generation from `AuthenticationService`. Have it delegate to `IUserTokenService`.

---

### 11. Wrong Namespace in Application Layer ✅ WILL FIX

**Severity:** 🟠 **HIGH**

**File:** [ConcurrentLocalStore.cs#L9](src/Application/LocalStore/Services/ConcurrentLocalStore.cs#L9)

```csharp
// File location: src/Application/LocalStore/Services/ConcurrentLocalStore.cs
namespace Infrastructure.Storage.Features;  // WRONG!
```

**Fix:** Change to `Application.LocalStore.Services`.

---

### 12. Claim Type Handling Inconsistency ✅ WILL FIX

**Severity:** 🟠 **HIGH**

📝 **Note:** Use shorter claim type names consistently.

**Problem:** Mixed use of short (`nameid`, `role`) and verbose (`ClaimTypes.NameIdentifier`, `ClaimTypes.Role`) claim types requires fallback chains everywhere:

```csharp
var userIdClaim = User.FindFirst("nameid") ?? User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
```

**Fix:** Use short claim types consistently. Remove all `??` fallback chains.

---

## 🟡 MEDIUM: Code Duplication (High Risk)

These duplications cause bugs when one copy changes but others don't.

### 13. Duplicate Token Hashing Functions (3 places) ✅ WILL FIX

**Files:**
- [AuthenticationService.cs#L195](src/Application/Identity/Services/AuthenticationService.cs#L195) - `ComputeHash()`
- [UserTokenService.cs#L153](src/Application/Identity/Services/UserTokenService.cs#L153) - `HashToken()`
- [AuthController.Token.cs#L106](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Token.cs#L106) - `HashToken()`

**Risk:** If one implementation changes, token validation breaks silently.

**Fix:** Create shared utility in `Application/Common/Services/HashingService.cs`.

---

### 14. Duplicate RBAC Version Constants (3 places) ✅ WILL FIX

**Files:**
- [JwtTokenService.cs#L18](src/Infrastructure.Identity/Services/JwtTokenService.cs#L18)
- [AuthenticationService.cs#L27](src/Application/Identity/Services/AuthenticationService.cs#L27)
- [PermissionService.cs#L28](src/Application/Authorization/Services/PermissionService.cs#L28)

```csharp
private const string CurrentRbacVersion = "2";  // Defined 3 times!
```

**Fix:** Define once in `Domain/Authorization/Constants/RbacConstants.cs`.

---

### 15. Duplicate Token Expiration Constants (2 places) ✅ WILL FIX

**Files:**
- [AuthenticationService.cs#L152](src/Application/Identity/Services/AuthenticationService.cs#L152) - `now.AddDays(7)`
- [UserTokenService.cs#L22](src/Application/Identity/Services/UserTokenService.cs#L22) - `TimeSpan.FromDays(7)`

**Risk:** Changing one doesn't affect the other → inconsistent session lifetimes.

---

### 16. Duplicate SessionIdClaimType Constant (3 places) ✅ WILL FIX

**Files:**
- [AuthController.cs#L41](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.cs#L41)
- [ConfigureJwtBearerOptions.cs#L14](src/Infrastructure.Identity/ConfigureOptions/ConfigureJwtBearerOptions.cs#L14)
- [UserTokenService.cs#L20](src/Application/Identity/Services/UserTokenService.cs#L20)

```csharp
private const string SessionIdClaimType = "sid";  // Defined 3 times!
```

---

### 17. ResolveRoleCodesAsync Duplicated (3 services) ✅ WILL FIX

**Files:**
- [UserRegistrationService.cs#L171](src/Application/Identity/Services/UserRegistrationService.cs#L171)
- [UserProfileService.cs#L168](src/Application/Identity/Services/UserProfileService.cs#L168)
- [AuthenticationService.cs#L201](src/Application/Identity/Services/AuthenticationService.cs#L201)

All three have **identical** implementations.

**Fix:** Move to `IUserRoleResolver` which already exists.

---

### 18. ValidateRoleParameters Duplicated (2 services) ✅ WILL FIX

**Files:**
- [UserRegistrationService.cs#L184](src/Application/Identity/Services/UserRegistrationService.cs#L184)
- [UserAuthorizationService.cs#L186](src/Application/Identity/Services/UserAuthorizationService.cs#L186)

**Fix:** Extract to shared helper in `Application/Authorization/Services/RoleValidation.cs`.

---

### 19. ResolveRoleCodesAsync Similar to GetFormattedRoleClaimsAsync ✅ WILL FIX

**File:** [AuthenticationService.cs](src/Application/Identity/Services/AuthenticationService.cs)

Nearly identical to `UserAuthorizationService.GetFormattedRoleClaimsAsync()`.

---

## 🟢 LOW: Code Duplication (Maintenance Burden)

### 20. ExpandScope Logic Duplicated ✅ WILL FIX

**Files:**
- [RoleDefinition.cs#L31](src/Domain/Authorization/Constants/RoleDefinition.cs#L31)
- [Role.cs#L94](src/Domain/Authorization/Models/Role.cs#L94)

---

### 21. Empty Dictionary Constants Duplicated ✅ WILL FIX

📝 **Note:** Use `StringStringDictionary`, `StringNullableStringDictionary` naming.

**Files:** Role.cs, UserRoleAssignment.cs, ScopeDirective.cs, Permission.cs

**Fix:** Create `Domain/Shared/Constants/EmptyCollections.cs`.

---

### 22. ParseRoleClaim / ParseIdentifier Pattern Duplication ✅ WILL FIX

**Files:**
- [Role.cs](src/Domain/Authorization/Models/Role.cs) - `ParseRoleClaim()`, `TryParseRoleClaim()`
- [Permission.cs](src/Domain/Authorization/Models/Permission.cs) - `ParseIdentifier()`, `TryParseIdentifier()`
- [ScopeDirective.cs](src/Domain/Authorization/ValueObjects/ScopeDirective.cs) - `Parse()`, `TryParse()`

All follow identical pattern. Could be generalized to shared parser.

---

### 23. Permission Tree Built Multiple Times ✅ WILL FIX

**Files:**
- [Permissions.cs](src/Domain/Authorization/Constants/Permissions.cs)
- [PermissionService.cs](src/Application/Authorization/Services/PermissionService.cs)
- [ScopeEvaluator.cs](src/Domain/Authorization/Services/ScopeEvaluator.cs)

All call `Permissions.GetAll()` and build lookup dictionaries. Should be single shared cache.

---

## ⚪ LOW: Code Quality / Technical Debt

### 24. Hydrate Methods Proliferation ✅ WILL FIX

📝 **Note:** Use `HydrationData` record pattern.

**Files:** [User.cs](src/Domain/Identity/Models/User.cs), [Role.cs](src/Domain/Authorization/Models/Role.cs)

`User.Hydrate()` has **19 parameters**.

**Fix:** Use `HydrationData` records:
```csharp
public sealed record UserHydrationData(...all properties...);
public static User Hydrate(UserHydrationData data) => ...
```

---

### 25. Pragma Warning Disable ✅ WILL FIX

**File:** [CmdService.cs#L16](src/Application/NativeCmd/Services/CmdService.cs#L16)

```csharp
#pragma warning disable CA1822
```

**Fix:** Remove pragma; make methods static or justify instance methods.

---

### 26. ConfigureJwtBearerOptions Blocking Async ⏭️ SKIP

**File:** [ConfigureJwtBearerOptions.cs#L80-L83](src/Infrastructure.Identity/ConfigureOptions/ConfigureJwtBearerOptions.cs#L80-L83)

```csharp
.GetTokenValidationParameters(CancellationToken.None)
.GetAwaiter()
.GetResult();  // Blocking call!
```

**Reason for SKIP:** `GetTokenValidationParameters` correctly remains async since it depends on async configuration factory. The blocking call in `ConfigureJwtBearerOptions` is acceptable because `IConfigureOptions<T>.Configure()` is synchronous by design (ASP.NET Core limitation). This is a known pattern for options configuration.

---

## ⏭️ SKIP

### 27. User Property Setters Pattern ⏭️ SKIP

**File:** [User.cs](src/Domain/Identity/Models/User.cs)

20+ near-identical setter methods. Acceptable as-is - explicit setters provide clear domain behavior.

---

## 📋 Final Summary

| Severity | Count | Issues |
|----------|-------|--------|
| 🔴 CRITICAL SECURITY | 3 | #1, #2, #3 |
| 🔴 CRITICAL BUGS | 4 | #4, #5, #6, #7 |
| 🟠 HIGH (Architecture) | 5 | #8, #9, #10, #11, #12 |
| 🟡 MEDIUM (Duplication - High Risk) | 7 | #13-19 |
| 🟢 LOW (Duplication - Maintenance) | 4 | #20-23 |
| ⚪ LOW (Code Quality) | 3 | #24-26 |
| ⏭️ SKIP | 1 | #27 |
| **Total** | **27** | **26 to fix, 1 skip** |

---

## Quick Reference: Priority Order

| Priority | # | Issue | Severity |
|----------|---|-------|----------|
| 1 | #1 | Password reset doesn't verify token | 🔴 SECURITY |
| 2 | #2 | 2FA code never verified | 🔴 SECURITY |
| 3 | #3 | Legacy tokens get full admin | 🔴 SECURITY |
| 4 | #4 | PasskeyService static dictionary | 🔴 BUG |
| 5 | #5 | User.SetEmail confirms null emails | 🔴 BUG |
| 6 | #6 | macOS NotImplementedException | 🔴 BUG |
| 7 | #7 | async void method | 🔴 BUG |
| 8 | #8 | Hashing in controller | 🟠 ARCH |
| 9 | #9 | PasskeyService session/token issues | 🟠 ARCH |
| 10 | #10 | Two token flows | 🟠 ARCH |
| 11 | #11 | Wrong namespace | 🟠 ARCH |
| 12 | #12 | Claim type inconsistency | 🟠 ARCH |
| 13 | #13 | Duplicate hashing (3x) | 🟡 DUP |
| 14 | #14 | Duplicate RBAC version (3x) | 🟡 DUP |
| 15 | #15 | Duplicate token expiration (2x) | 🟡 DUP |
| 16 | #16 | Duplicate SessionIdClaimType (3x) | 🟡 DUP |
| 17 | #17 | Duplicate ResolveRoleCodesAsync (3x) | 🟡 DUP |
| 18 | #18 | Duplicate ValidateRoleParameters (2x) | 🟡 DUP |
| 19 | #19 | Similar role resolution methods | 🟡 DUP |
| 20 | #20 | ExpandScope duplication | 🟢 DUP |
| 21 | #21 | Empty dictionary duplication | 🟢 DUP |
| 22 | #22 | Parse pattern duplication | 🟢 DUP |
| 23 | #23 | Permission tree multiple builds | 🟢 DUP |
| 24 | #24 | Hydrate methods 19 params | ⚪ QUALITY |
| 25 | #25 | Pragma warning disable | ⚪ QUALITY |
| 26 | #26 | Blocking async in config | ⚪ QUALITY |
| - | #27 | User setters pattern | ⏭️ SKIP |

---

## 🟠 HIGH: API-to-Service Flow Issues (Pass 4)

These are "duck tape" implementations where business logic lives in the Presentation layer instead of services.

### 28. Password Confirmation Validation in Controller ✅ WILL FIX

**Severity:** 🟠 **HIGH**

**File:** [AuthController.Login.cs#L114-L126](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Login.cs#L114-L126)

**Problem:** Registration password confirmation validation is done directly in the controller:

```csharp
if (request.Password != request.ConfirmPassword)
{
    return BadRequest(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Passwords do not match",
        Detail = "Password and ConfirmPassword must match."
    });
}
```

**Why this is wrong:** Input validation logic belongs in Application layer services or validators, not controllers.

**Fix:** Move validation to `IUserRegistrationService.RegisterUserAsync()` or create `IRegistrationValidator`.

---

### 29. Username/Email Uniqueness Checks in Controller ✅ WILL FIX

**Severity:** 🟠 **HIGH**

**Files:**
- [AuthController.Login.cs#L161-L183](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Login.cs#L161-L183)
- [AuthController.Identity.cs#L117-L148](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Identity.cs#L117-L148)
- [AuthController.Identity.cs#L210-L224](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Identity.cs#L210-L224)
- [AuthController.Identity.cs#L425-L440](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Identity.cs#L425-L440)

**Problem:** Controllers directly query `userProfileService.GetByUsernameAsync()` and `GetByEmailAsync()` to check uniqueness before delegating to services:

```csharp
// In Register endpoint
var existingUser = await userProfileService.GetByUsernameAsync(request.Username!, cancellationToken);
if (existingUser is not null)
{
    return Conflict(new ProblemDetails { ... });
}
```

This pattern appears **4+ times** across different controller methods.

**Why this is wrong:** 
1. Duplicated validation logic across multiple endpoints
2. Service layer should enforce uniqueness constraints and throw `DuplicateEntityException`
3. Race condition: user could be created between check and registration

**Fix:** Services should validate uniqueness internally and throw `DuplicateEntityException`. Controllers just catch and map to 409.

---

### 30. User-Agent Parsing in Controller ✅ WILL FIX

**Severity:** 🟠 **HIGH**

**File:** [AuthController.Helpers.cs#L80-L118](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Helpers.cs#L80-L118)

**Problem:** 40+ lines of User-Agent string parsing logic hardcoded in controller:

```csharp
private static string? ParseDeviceName(string? userAgent)
{
    if (string.IsNullOrWhiteSpace(userAgent)) return null;
    
    if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
    {
        if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
            return "Chrome on Windows";
        // ... 20+ more cases
    }
    // ... more OS detection
}
```

**Why this is wrong:**
1. Business logic in Presentation layer
2. Should be in Application layer (e.g., `IDeviceInfoParser`)
3. Hard to test in isolation
4. Comment says "in production, use a proper UA parser library"

**Fix:** Create `Application/Common/Services/DeviceInfoParser.cs` implementing `IDeviceInfoParser`.

---

### 31. Password Reset URL Construction in Controller ✅ WILL FIX

**Severity:** 🟠 **HIGH**

**File:** [AuthController.Password.cs#L81-L84](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Password.cs#L81-L84)

**Problem:** Controller constructs password reset URL directly:

```csharp
var resetLink = $"{Request.Scheme}://{Request.Host}/reset-password?email={Uri.EscapeDataString(request.Email)}&token={encodedToken}";

await emailService.SendPasswordResetLinkAsync(request.Email, resetLink, cancellationToken);
```

**Why this is wrong:**
1. URL construction logic belongs in service or configuration
2. Hardcoded path `/reset-password` - what if frontend uses different route?
3. Comment says "In production, use a proper base URL from configuration"

**Fix:** Service should construct URL using injected `IUrlConfiguration` or `IFrontendUrlBuilder`.

---

### 32. "Last Auth Method" Check Logic in Controllers ✅ WILL FIX

**Severity:** 🟠 **HIGH**

**Files:**
- [AuthController.Passkey.cs#L311-L319](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Passkey.cs#L311-L319)
- [AuthController.Identity.cs#L355-L366](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Identity.cs#L355-L366)

**Problem:** Business rule "cannot remove last authentication method" is implemented in controllers:

```csharp
// In RevokePasskey
var passkeys = await passkeyService.ListPasskeysAsync(userId, cancellationToken);
if (!user.HasPassword && oauthProviders == 0 && passkeys.Count <= 1)
{
    return BadRequest(new ProblemDetails
    {
        Title = "Cannot unlink last authentication method",
        ...
    });
}
```

This logic appears in **multiple places** for passkey deletion and OAuth unlinking.

**Why this is wrong:**
1. Duplicated business rule across endpoints
2. Services should enforce this invariant, not controllers
3. Easy to forget adding this check when creating new unlink endpoints

**Fix:** Create `IAuthMethodGuardService.CanRemoveAuthMethodAsync(userId, AuthMethodType)` or have each unlink service method validate internally.

---

### 33. OAuth Username Generation in Controller ✅ WILL FIX

**Severity:** 🟡 **MEDIUM**

**File:** [AuthController.OAuth.cs#L250-L261](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.OAuth.cs#L250-L261)

**Problem:** Username generation from OAuth info is in controller:

```csharp
private static string GenerateUsernameFromOAuth(OAuthUserInfo userInfo)
{
    var baseUsername = userInfo.Name?.Replace(" ", "").ToLowerInvariant()
        ?? userInfo.Email?.Split('@')[0].ToLowerInvariant()
        ?? $"user_{userInfo.ProviderSubject[..Math.Min(8, userInfo.ProviderSubject.Length)]}";

    var suffix = Guid.NewGuid().ToString("N")[..6];
    return $"{baseUsername}_{suffix}";
}
```

**Why this is wrong:** Username generation is a business rule that belongs in `IUserRegistrationService` or a dedicated `IUsernameGenerator`.

**Fix:** Move to Application layer service.

---

### 34. OAuth Callback User Lookup Logic in Controller ✅ WILL FIX

**Severity:** 🟡 **MEDIUM**

**File:** [AuthController.OAuth.cs#L140-L175](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.OAuth.cs#L140-L175)

**Problem:** The OAuth callback endpoint contains complex user lookup logic:

```csharp
// Check if user already exists with this external login
var user = await userProfileService.GetByIdAsync(Guid.Empty, cancellationToken); // Placeholder!
// ... 
var existingByEmail = !string.IsNullOrEmpty(userInfo.Email) && userInfo.EmailVerified
    ? await userProfileService.GetByEmailAsync(userInfo.Email, cancellationToken)
    : null;

// Complex logic to determine if new user or existing...
```

**Why this is wrong:**
1. Contains a `// Placeholder` comment - incomplete implementation
2. Controller orchestrates complex user lookup logic
3. Should be single service call: `oauthService.ProcessCallbackAndGetOrCreateUserAsync()`

**Fix:** `IOAuthService.ProcessCallbackAsync()` should return the user (existing or newly created), not just validation result.

---

### 35. Session-to-User Validation in Controller ✅ WILL FIX

**Severity:** 🟡 **MEDIUM**

**File:** [AuthController.Sessions.cs#L63-L73](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Sessions.cs#L63-L73)

**Problem:** Controller validates that session belongs to user:

```csharp
var session = await sessionService.GetByIdAsync(id, cancellationToken);
if (session is null || session.UserId != userId)
{
    return NotFound(new ProblemDetails { ... });
}
```

**Why this is wrong:** Authorization check belongs in service layer. Service should have `RevokeAsync(userId, sessionId)` that validates ownership.

**Fix:** `ISessionService.RevokeAsync()` should take `userId` and validate ownership internally.

---

### 36. Token Refresh Permission Check in Controller ✅ WILL FIX

**Severity:** 🟠 **HIGH**

**File:** [AuthController.Token.cs#L37-L84](src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Token.cs#L37-L84)

**Problem:** The refresh endpoint contains extensive business logic:
1. Token validation
2. User ID extraction with multiple claim type fallbacks
3. Permission verification (`HasPermissionAsync`)
4. Session ID extraction
5. Session validation with token hash comparison

```csharp
var principal = await permissionService.ValidateTokenAsync(request.RefreshToken, cancellationToken);
// ... 20+ lines of extraction and validation ...
var tokenHash = HashToken(request.RefreshToken);
var loginSession = await sessionService.ValidateSessionAsync(sessionId, tokenHash, cancellationToken);
```

**Why this is wrong:** All of this should be a single service call like:
```csharp
var result = await userTokenService.RefreshTokensAsync(request.RefreshToken, cancellationToken);
```

**Fix:** Move all validation logic into `IUserTokenService.RefreshTokensAsync()`. Controller should just map result to response.

---

## 📋 Updated Final Summary

| Severity | Count | Issues |
|----------|-------|--------|
| 🔴 CRITICAL SECURITY | 3 | #1, #2, #3 |
| 🔴 CRITICAL BUGS | 4 | #4, #5, #6, #7 |
| 🟠 HIGH (Architecture) | 5 | #8, #9, #10, #11, #12 |
| 🟠 HIGH (API-to-Service Flow) | 7 | #28, #29, #30, #31, #32, #35, #36 |
| 🟡 MEDIUM (Duplication - High Risk) | 7 | #13-19 |
| 🟡 MEDIUM (API-to-Service Flow) | 2 | #33, #34 |
| 🟢 LOW (Duplication - Maintenance) | 4 | #20-23 |
| ⚪ LOW (Code Quality) | 2 | #24-25 |
| ⏭️ SKIP | 2 | #26, #27 |
| **Total** | **36** | **34 to fix, 2 skip** |

---

## Quick Reference: Priority Order (Updated)

| Priority | # | Issue | Severity |
|----------|---|-------|----------|
| 1 | #1 | Password reset doesn't verify token | 🔴 SECURITY |
| 2 | #2 | 2FA code never verified | 🔴 SECURITY |
| 3 | #3 | Legacy tokens get full admin | 🔴 SECURITY |
| 4 | #4 | PasskeyService static dictionary | 🔴 BUG |
| 5 | #5 | User.SetEmail confirms null emails | 🔴 BUG |
| 6 | #6 | macOS NotImplementedException | 🔴 BUG |
| 7 | #7 | async void method | 🔴 BUG |
| 8 | #8 | Hashing in controller | 🟠 ARCH |
| 9 | #9 | PasskeyService session/token issues | 🟠 ARCH |
| 10 | #10 | Two token flows | 🟠 ARCH |
| 11 | #11 | Wrong namespace | 🟠 ARCH |
| 12 | #12 | Claim type inconsistency | 🟠 ARCH |
| 13 | #36 | Token refresh logic in controller | 🟠 API→SVC |
| 14 | #28 | Password confirmation in controller | 🟠 API→SVC |
| 15 | #29 | Uniqueness checks in controller (4x) | 🟠 API→SVC |
| 16 | #30 | User-Agent parsing in controller | 🟠 API→SVC |
| 17 | #31 | Reset URL construction in controller | 🟠 API→SVC |
| 18 | #32 | Last auth method check in controllers | 🟠 API→SVC |
| 19 | #35 | Session ownership check in controller | 🟡 API→SVC |
| 20 | #13 | Duplicate hashing (3x) | 🟡 DUP |
| 21 | #14 | Duplicate RBAC version (3x) | 🟡 DUP |
| 22 | #15 | Duplicate token expiration (2x) | 🟡 DUP |
| 23 | #16 | Duplicate SessionIdClaimType (3x) | 🟡 DUP |
| 24 | #17 | Duplicate ResolveRoleCodesAsync (3x) | 🟡 DUP |
| 25 | #18 | Duplicate ValidateRoleParameters (2x) | 🟡 DUP |
| 26 | #19 | Similar role resolution methods | 🟡 DUP |
| 27 | #33 | OAuth username generation | 🟡 API→SVC |
| 28 | #34 | OAuth callback user lookup | 🟡 API→SVC |
| 29 | #20 | ExpandScope duplication | 🟢 DUP |
| 30 | #21 | Empty dictionary duplication | 🟢 DUP |
| 31 | #22 | Parse pattern duplication | 🟢 DUP |
| 32 | #23 | Permission tree multiple builds | 🟢 DUP |
| 33 | #24 | Hydrate methods 19 params | ⚪ QUALITY |
| 34 | #25 | Pragma warning disable | ⚪ QUALITY |
| - | #26 | Blocking async in config | ⏭️ SKIP |
| - | #27 | User setters pattern | ⏭️ SKIP |

---

*Generated from deep code analysis (4 passes). Merged and ranked by severity.*
*Pass 4 focused on API-to-Service flow violations.*
