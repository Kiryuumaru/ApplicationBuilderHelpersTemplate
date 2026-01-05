# TODO: Architecture Improvements

This document tracks remaining architecture improvements and the new inline role parameters feature.

---

## ✅ Completed Items

The following items from the original TODO have been implemented:

### Folder & Namespace Reorganization ✅
- `Application/Identity/Interfaces/Infrastructure/` - Created
- `Application/Authorization/Interfaces/Infrastructure/` - Created
- `Application/Authorization/Interfaces/Application/` - Created
- `Application/Identity/Models/` - Created
- `Application/Authorization/Models/` - Created
- `InternalsVisibleTo` in `Application/AssemblyInfo.cs` - Added

### Interface Split (IIdentityService → Focused Services) ✅
- `IUserRegistrationService` - Created (4 methods)
- `IAuthenticationService` - Created (4 methods)
- `IPasswordService` - Created (5 methods)
- `ITwoFactorService` - Created (6 methods)
- `IUserProfileService` - Created (9 methods)
- `IUserRoleAssignmentService` - Created (replaces IUserRoleService)
- `IPasskeyService` - Kept
- `IOAuthService` - Kept
- `IAnonymousUserCleanupService` - Kept

### Repository Interfaces (Internal) ✅
- `IUserRepository` - Created (merged IUserStore + IExternalLoginStore)
- `ISessionRepository` - Created (renamed from ISessionStore)
- `IPasskeyRepository` - Created (merged IPasskeyCredentialStore + IPasskeyChallengeStore)
- `IRoleRepository` - Created (absorbed IRoleLookup)
- All marked as `internal` ✅

### DTOs ✅
- `UserDto` - Created in `Application.Identity.Models`
- `UserSessionDto` - Created in `Application.Identity.Models`
- `AuthenticationResultDto` - Created in `Application.Identity.Models`
- `TokenValidationResult` - Created in `Application.Authorization.Models`

### Token Service ✅
- `ITokenService` - Created as public interface

### Deleted Items ✅
- `IRoleLookup` - Deleted (absorbed by IRoleRepository)
- `IPermissionServiceFactory` - Deleted
- `IIdentityService` - Deleted (split into focused services)
- Granular store interfaces - Deleted (merged into repositories)

### JWT Interfaces ✅
- `IJwtTokenService` - Moved to `Infrastructure.Identity` as `internal`

---

## 🔄 Remaining Items

### Phase 5: ASP.NET Identity Abstraction (Partial)

Some Application layer services still directly use ASP.NET Identity types:

- [ ] **`PasswordService`** uses `UserManager<User>` directly for password validation
  - Create `IPasswordValidator` interface in Application layer
  - Implement in Infrastructure using ASP.NET Identity's validators
  - Remove `UserManager<User>` dependency from `PasswordService`

- [ ] **`IPasswordHasher<User>`** from ASP.NET Identity used directly
  - Already have `IPasswordVerifier` abstraction ✅
  - Consider creating `IPasswordHasher` abstraction if needed for consistency

### Phase 6: Domain Enrichment (Optional/Future)

- [ ] Add value objects: `Username`, `Email`, `Password`
- [ ] Add domain events for user lifecycle
- [ ] Move more business rules to `User` entity
- [ ] Create domain services for complex operations

---

## 🆕 Feature: Inline Role Parameters in JWT Claims

### Goal

Change the JWT token format to embed role parameters inline with the role code, removing separate `role_params:` claims and role-derived scopes from the token.

### Current Format (Before)

```json
{
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "USER",
  "role_params:USER": "{\"roleUserId\":\"abc123\"}",
  "scope": [
    "allow;_read;userId=abc123",
    "allow;_write;userId=abc123",
    "allow;api:bots:strategies:_read",
    "deny;api:auth:refresh"
  ],
  "rbac_version": "2"
}
```

### New Format (After)

```json
{
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": [
    "USER;roleUserId=abc123",
    "CUSTOM_ROLE;customParam=value"
  ],
  "scope": [
    "allow;manually_added_scope:_read"
  ],
  "rbac_version": "2"
}
```

### Key Changes

#### 1. Role Claim Format

- **Before**: `role: "USER"` + `role_params:USER: {"roleUserId": "abc123"}`
- **After**: `role: "USER;roleUserId=abc123"`

Format: `{CODE};{param1}={value1};{param2}={value2}`

#### 2. Scope Claims

- **Before**: Token contains role-expanded scopes (e.g., `allow;_read;userId=abc123`)
- **After**: Token contains ONLY manually-added scopes (via API)
- Role scopes are resolved at runtime from database, not stored in token

#### 3. Runtime Resolution

Permission checking already resolves roles at runtime:
1. Extract role codes + params from `role` claims
2. Fetch current role definitions from database
3. Expand scope templates with parameter values
4. Evaluate permission against expanded scopes

### Files to Modify

#### Domain Layer

- [x] `Domain/Authorization/Models/Role.cs` (MODIFY)
  - Add `ParsedRoleClaim` record struct (similar to `Permission.ParsedIdentifier`)
  - Add `static ParseRoleClaim(string claim)` - parse `USER;roleUserId=abc123`
  - Add `static TryParseRoleClaim(string claim, out ParsedRoleClaim parsed)`
  - Add `static FormatRoleClaim(string code, IReadOnlyDictionary<string, string?>? parameters)` - format to string
  - `ParsedRoleClaim.ToString()` - format back to string
  - Properties: `Original`, `Code`, `Parameters`

#### Application Layer

- [x] `Application/Identity/Interfaces/IUserAuthorizationService.cs` (MODIFY)
  - Add `GetFormattedRoleClaimsAsync` - returns inline role claims with parameters
  - Add `GetDirectPermissionScopesAsync` - returns only direct grants, not role-derived scopes

- [x] `Application/Identity/Services/UserAuthorizationService.cs` (MODIFY)
  - Implement `GetFormattedRoleClaimsAsync` using `Role.FormatRoleClaim()`
  - Implement `GetDirectPermissionScopesAsync` returning only `PermissionGrants`

- [x] `Application/Authorization/Services/PermissionService.cs` (MODIFY)
  - Update `ResolveScopeDirectivesAsync` to support both `ClaimTypes.Role` and `"role"` claim types
  - Parse inline role format using `Role.TryParseRoleClaim()`

#### Presentation Layer

- [x] `Presentation.WebApi/Controllers/V1/Auth/AuthController.Helpers.cs` (MODIFY)
  - Update `GenerateAccessTokenForSessionAsync` to use inline role format
  - Use `GetFormattedRoleClaimsAsync` for role claims with parameters
  - Use `GetDirectPermissionScopesAsync` for scopes (NOT role-derived)
  - Use short `"role"` claim type instead of verbose `ClaimTypes.Role`

### Implementation Steps

1. ✅ Add `ParsedRoleClaim`, `ParseRoleClaim`, `TryParseRoleClaim`, and `FormatRoleClaim` to `Role.cs` in Domain layer
2. ✅ Add `GetFormattedRoleClaimsAsync` and `GetDirectPermissionScopesAsync` to `IUserAuthorizationService`
3. ✅ Update `AuthController.Helpers.cs`:
   - Use formatted role claims with inline parameters
   - Only add direct permission grants to scopes (not role-derived)
4. ✅ Update `PermissionService.ResolveScopeDirectivesAsync`:
   - Support both short and verbose role claim types
   - Parse inline role format using `Role.TryParseRoleClaim()`
5. ✅ Build and run all tests (497 tests pass)

### Benefits

1. **Simpler token structure** - No separate `role_params:` claims
2. **Smaller tokens** - No role-derived scopes in token
3. **Cleaner format** - Similar to scope directive format
4. **Immediate effect** - Role changes apply without token regeneration

---

## 🧪 Comprehensive Test Cases for Inline Role Parameters

### Domain Layer Tests (`Domain.UnitTests/Authorization/RoleParsingTests.cs`)

#### Basic Parsing Tests

- [ ] `ParseRoleClaim_CodeOnly_ReturnsCodeWithEmptyParams` - `"USER"` → Code=`USER`, Params={}
- [ ] `ParseRoleClaim_SingleParam_ParsesCorrectly` - `"USER;roleUserId=abc123"` → Code=`USER`, Params={roleUserId: abc123}
- [ ] `ParseRoleClaim_MultipleParams_ParsesAllParams` - `"ADMIN;orgId=org1;teamId=team2"` → both params extracted
- [ ] `ParseRoleClaim_ParamsOrderIndependent` - `"ROLE;b=2;a=1"` parses same as `"ROLE;a=1;b=2"`
- [ ] `ParseRoleClaim_CodeNormalized_ToUppercase` - `"user;param=val"` → Code=`USER`
- [ ] `TryParseRoleClaim_ValidInput_ReturnsTrue` - valid claim returns true
- [ ] `TryParseRoleClaim_InvalidInput_ReturnsFalse` - invalid claim returns false without throwing

#### Edge Cases

- [ ] `ParseRoleClaim_EmptyString_Throws` - `""` throws FormatException
- [ ] `ParseRoleClaim_WhitespaceOnly_Throws` - `"   "` throws FormatException
- [ ] `ParseRoleClaim_Null_ThrowsArgumentNull` - null throws ArgumentNullException
- [ ] `ParseRoleClaim_TrailingSemicolon_IgnoresEmpty` - `"USER;param=val;"` handles gracefully
- [ ] `ParseRoleClaim_LeadingSemicolon_Throws` - `";USER"` throws (no code before semicolon)
- [ ] `ParseRoleClaim_EmptyParamName_Throws` - `"USER;=value"` throws FormatException
- [ ] `ParseRoleClaim_EmptyParamValue_Throws` - `"USER;param="` throws FormatException
- [ ] `ParseRoleClaim_MissingEquals_Throws` - `"USER;paramvalue"` throws FormatException
- [ ] `ParseRoleClaim_MultipleEquals_UsesFirstSplit` - `"USER;param=val=ue"` → param=`val=ue`
- [ ] `ParseRoleClaim_DuplicateParamNames_LastWins` - `"USER;a=1;a=2"` → a=`2`
- [ ] `ParseRoleClaim_WhitespaceAroundParts_Trimmed` - `" USER ; param = value "` → Code=`USER`, param=`value`

#### Special Characters (Security-Critical)

- [ ] `ParseRoleClaim_ParamValueWithColon_Preserved` - `"USER;url=http://example.com"` → url contains colon
- [ ] `ParseRoleClaim_ParamValueWithSlash_Preserved` - `"USER;path=/api/v1"` → path preserved
- [ ] `ParseRoleClaim_ParamValueIsGuid_Preserved` - `"USER;id=550e8400-e29b-41d4-a716-446655440000"`
- [ ] `ParseRoleClaim_ParamValueWithSpaces_Preserved` - `"USER;name=John Doe"` (if spaces allowed)
- [ ] `ParseRoleClaim_ParamValueUrlEncoded_NotDecoded` - `"USER;val=%20%3D"` stored as-is (no auto-decode)

#### Formatting Tests

- [ ] `FormatRoleClaim_CodeOnly_ReturnsCode` - Code=`USER`, Params={} → `"USER"`
- [ ] `FormatRoleClaim_WithParams_FormatsCorrectly` - Code=`USER`, {a:1, b:2} → `"USER;a=1;b=2"`
- [ ] `FormatRoleClaim_ParamsSortedAlphabetically` - {z:1, a:2} → `"ROLE;a=2;z=1"`
- [ ] `FormatRoleClaim_NullParams_TreatedAsEmpty` - Code=`USER`, Params=null → `"USER"`
- [ ] `ParsedRoleClaim_ToString_Roundtrips` - Parse then ToString returns equivalent string
- [ ] `FormatRoleClaim_EmptyCode_Throws` - empty code throws

### Security Tests (`Domain.UnitTests/Authorization/RoleClaimSecurityTests.cs`)

#### Injection Prevention

- [ ] `ParseRoleClaim_SqlInjectionInParam_StoredAsLiteral` - `"USER;id='; DROP TABLE--"` stored literally
- [ ] `ParseRoleClaim_ScriptInjectionInParam_StoredAsLiteral` - `"USER;name=<script>alert(1)</script>"`
- [ ] `ParseRoleClaim_JsonInjectionInParam_StoredAsLiteral` - `"USER;data={\"evil\":true}"`
- [ ] `ParseRoleClaim_NewlineInParam_Rejected` - `"USER;val=line1\nline2"` throws or sanitized
- [ ] `ParseRoleClaim_NullByteInParam_Rejected` - `"USER;val=test\0evil"` throws or sanitized
- [ ] `ParseRoleClaim_ControlCharsInParam_Rejected` - control characters rejected

#### Parameter Tampering Prevention

- [ ] `RoleExpansion_WrongUserId_DeniesAccess` - USER role with userId=X cannot access userId=Y resources
- [ ] `RoleExpansion_MissingRequiredParam_NoPermissionGranted` - role needing param without it grants nothing
- [ ] `RoleExpansion_ExtraParams_Ignored` - extra params not in template are ignored
- [ ] `RoleExpansion_ParamCaseSensitive` - `userId` vs `UserId` treated as different params

#### Token Manipulation

- [ ] `Permission_TokenWithFakeRole_DeniedIfRoleNotInDb` - invented role code not in DB grants nothing
- [ ] `Permission_TokenWithTamperedParams_OnlyDbRoleMatters` - params only matter for template expansion
- [ ] `Permission_ModifiedRoleInToken_DbLookupPrevails` - even if token claims ADMIN, DB lookup determines truth

### Integration Tests (`Presentation.WebApi.FunctionalTests/Iam/`)

#### Token Format Verification

- [ ] `Login_ReturnsTokenWithInlineRoleParams` - verify new token format
- [ ] `Login_NoSeparateRoleParamsClaims` - verify `role_params:` claims absent
- [ ] `Login_OnlyManualScopesInToken` - verify role-derived scopes not in token
- [ ] `AnonymousAuth_TokenHasUserRoleWithUserId` - `"USER;roleUserId={userId}"` format

#### Permission Resolution

- [ ] `ModifyRole_TakesEffectWithoutReLogin` - (existing) still passes
- [ ] `UserWithParametrizedRole_CanAccessOwnResources` - USER;roleUserId=X can access /users/X
- [ ] `UserWithParametrizedRole_CannotAccessOtherResources` - USER;roleUserId=X cannot access /users/Y
- [ ] `RoleWithMultipleParams_AllParamsExpanded` - role with orgId + teamId expands both
- [ ] `RoleWithoutParams_WorksNormally` - ADMIN role (no params) works
- [ ] `MultipleRoles_AllExpanded` - user with USER + MODERATOR gets both roles' permissions

#### Edge Cases in Real Flow

- [ ] `RoleDeleted_UserLosesAccess` - delete role → user with that role loses permissions
- [ ] `RoleParamMismatch_NoPartialExpansion` - if role needs param X but token has param Y, no expansion
- [ ] `EmptyRoleList_OnlyScopeClaimsUsed` - user with no roles but manual scopes still works
- [ ] `LegacyToken_StillWorks` - rbac_version=1 or missing still grants admin (backward compat)

### Backward Compatibility Tests

- [ ] `OldTokenFormat_StillParsed` - tokens with `role_params:` claims still work during migration
- [ ] `MixedTokenFormat_BothParsed` - token with both old and new format handles gracefully
- [ ] `RbacVersion2_RequiresNewFormat` - version 2 tokens use new parsing logic

### Performance Tests (Optional)

- [ ] `ParseRoleClaim_1000Iterations_Under1ms` - parsing performance acceptable
- [ ] `ResolveScopeDirectives_ManyRoles_Efficient` - 10+ roles doesn't degrade significantly
- [ ] `DbLookup_Cached_NotCalledRepeatedly` - role lookup caching works (if implemented)

---

## Summary

| Category | Status |
|----------|--------|
| Folder Structure | ✅ Complete |
| Interface Split | ✅ Complete |
| Repository Interfaces | ✅ Complete |
| DTOs | ✅ Complete |
| ITokenService | ✅ Complete |
| Cleanup (deleted old interfaces) | ✅ Complete |
| ASP.NET Identity Abstraction | 🔄 Partial (PasswordService still uses UserManager) |
| Domain Enrichment | ⏳ Future/Optional |
| **Inline Role Parameters** | ✅ Complete (core implementation) |
