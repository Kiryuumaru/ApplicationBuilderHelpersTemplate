# API Key Management Implementation Plan

## Overview

Implement user-managed API keys that mirror the user's access permissions with specific restrictions. API keys are **JWTs** with `typ: ak+jwt` that provide programmatic access for bots, scripts, and integrations without requiring interactive login sessions.

**Key Differences from Access Tokens:**
- ✅ Same roles and scopes (mirrors user permissions)
- ❌ Cannot refresh (no `api:auth:refresh` permission)
- ❌ Cannot manage API keys (no `api:auth:api_keys:*` permissions)

## Token Type Summary

| Token Type | JWT `typ` | Lifetime | Use Case |
|------------|-----------|----------|----------|
| `Access` | `at+jwt` | Short (15-60 min) | Interactive API calls |
| `Refresh` | `rt+jwt` | Long (7-30 days) | Obtain new access tokens |
| `ApiKey` | `ak+jwt` | Configurable (months/years/never) | Programmatic/bot access |

### Current JWT Token Structure

All tokens follow the same structure but with different claims:

**Access Token (`at+jwt`):**
```json
{
  "sub": "user-id",
  "name": "username",
  "jti": "random-guid",           // Random, not used for lookup
  "sid": "session-id",            // Session ID for revocation lookup
  "iat": 1704844800,
  "exp": 1704848400,
  "rbac_version": "2",
  "roles": ["USER;roleUserId=user-id"],  // Role codes with inline params
  "scope": [                      // ONLY direct grants + explicit denies
    "deny;api:auth:refresh;userId=user-id"  // Prevent access token from refreshing
  ]
}
```

**Refresh Token (`rt+jwt`):**
```json
{
  "sub": "user-id",
  "name": "username",
  "jti": "random-guid",
  "sid": "session-id",            // Same session ID as access token
  "iat": 1704844800,
  "exp": 1705449600,
  "rbac_version": "2",
  "scope": [
    "allow;api:auth:refresh;userId=user-id"  // ONLY permission
  ]
}
```

**API Key Token (`ak+jwt`) - NEW:**
```json
{
  "sub": "user-id",
  "name": "username",
  "jti": "apikey-id",             // API key ID for revocation lookup
  // NO "sid" - API keys are not session-based
  "iat": 1704844800,
  "exp": 1736380800,              // Optional, can be far future
  "rbac_version": "2",
  "roles": ["USER;roleUserId=user-id"],  // Same roles as user
  "scope": [
    "deny;api:auth:refresh;userId=user-id",
    "deny;api:auth:api_keys:_read;userId=user-id",   // Denies list
    "deny;api:auth:api_keys:_write;userId=user-id"   // Denies create, revoke
  ]
}
```

**Key Design Principle: Runtime Permission Resolution**

Tokens do NOT embed all permissions. Instead:
1. `roles` claim contains role codes with inline parameters
2. `scope` claim contains only direct grants and explicit denies
3. At request time, `IPermissionService.HasPermissionAsync` resolves role scopes from the database
4. This allows role changes to take effect immediately without token regeneration

## Architecture

```
User (userId: abc123)
  ├── Sessions (login sessions with access/refresh tokens)
  │     └── Session 1, Session 2, ...
  │
  └── API Keys (JWTs independent of sessions)
        ├── "Trading Bot" (id: key1, JWT with typ: ak+jwt)
        └── "CI/CD Pipeline" (id: key2, JWT with typ: ak+jwt)
```

**Key principle:** 
- API keys ARE JWTs (not opaque strings)
- Database stores **metadata** for revocation tracking, not the key itself
- Permissions resolved at request time from user's current roles (mirror access)
- **Unified validation pipeline** for all token types (access, refresh, api key)

### Unified JWT Validation Flow

All three token types go through the **same validation pipeline**:

```
1. VALIDATE (Unified for all tokens):
   → Verify JWT signature
   → Check standard claims (exp, iss, aud)
   → Read typ header
   → Type-specific post-validation:
     
     typ: at+jwt (Access Token)
     → No additional checks
     
     typ: ak+jwt (API Key)
     → Extract jti → DB lookup (IsRevoked? Expired?)
     → Update LastUsedAt
     
     typ: rt+jwt (Refresh Token)
     → Verify endpoint is POST /api/v1/auth/refresh
     → Reject if used on any other endpoint

2. CREATE API KEY:
   → Generate JWT with typ: ak+jwt (jti = API key ID)
   → Store metadata in DB (id=jti, userId, name, expiresAt)
   → Return JWT to user (shown once)

3. REVOKE API KEY:
   → Set IsRevoked = true in DB
   → JWT signature still valid but rejected on lookup
```

### Token Validation Flow by `typ`

```
                    ┌─────────────────────┐
                    │  JWT Bearer Token   │
                    └──────────┬──────────┘
                               │
                    ┌──────────▼──────────┐
                    │ Verify Signature    │
                    │ Check exp, iss, aud │
                    └──────────┬──────────┘
                               │
                    ┌──────────▼──────────┐
                    │  Read typ header    │
                    └──────────┬──────────┘
                               │
          ┌────────────────────┼────────────────────┐
          │                    │                    │
    ┌─────▼─────┐        ┌─────▼─────┐        ┌─────▼─────┐
    │ at+jwt    │        │ ak+jwt    │        │ rt+jwt    │
    │ (Access)  │        │ (API Key) │        │ (Refresh) │
    └─────┬─────┘        └─────┬─────┘        └─────┬─────┘
          │                    │                    │
          ▼                    ▼                    │
    ┌───────────┐        ┌───────────┐              │
    │ Session   │        │ DB Lookup │         Only valid
    │ Validation│        │ (jti)     │         for /refresh
    │ (optional)│        │           │         endpoint
    └─────┬─────┘        └─────┬─────┘              │
          │                    │                    ▼
          │              ┌─────▼─────┐        ┌───────────┐
          │              │IsRevoked? │        │  Reject   │
          │              │Expired?   │        │  (401)    │
          │              └─────┬─────┘        └───────────┘
          │                    │
          │              ┌─────▼─────┐
          │              │ Update    │
          │              │ LastUsedAt│
          │              └─────┬─────┘
          │                    │
          └────────┬───────────┘
                   │
          ┌────────▼────────┐
          │ Extract claims  │
          │ (sub, scope,    │
          │  roles, etc.)   │
          └────────┬────────┘
                   │
          ┌────────▼────────┐
          │ Continue with   │
          │ request         │
          └─────────────────┘
```

---

## Phase 1: Domain Layer

### 1.1 Create `ApiKey` Entity

**File:** `src/Domain/Identity/Entities/ApiKey.cs`

```csharp
public class ApiKey
{
    public string Id { get; set; }              // Primary key (GUID), also embedded in JWT as claim
    public string UserId { get; set; }          // Owner
    public string Name { get; set; }            // User-friendly name ("Trading Bot")
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }  // null = never expires (also in JWT exp claim)
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public bool IsRevoked { get; set; }
}
```

Note: No `KeyHash` needed - the API key IS a JWT. We only store metadata for revocation tracking.

### 1.2 Create Domain Exceptions

**File:** `src/Domain/Identity/Exceptions/ApiKeyNotFoundException.cs`
**File:** `src/Domain/Identity/Exceptions/ApiKeyRevokedException.cs`

---

## Phase 2: Application Layer

### 2.1 Create Repository Interface

**File:** `src/Application/Identity/Interfaces/Infrastructure/IApiKeyRepository.cs`

```csharp
public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<int> GetActiveCountByUserIdAsync(string userId, CancellationToken ct = default);
    Task CreateAsync(ApiKey apiKey, CancellationToken ct = default);
    Task UpdateAsync(ApiKey apiKey, CancellationToken ct = default);
}
```

Note: No `GetByKeyHashAsync` - we look up by `jti` claim (standard JWT ID, RFC 7519), not by hashing the key.
No `DeleteAsync` - we use soft delete via `IsRevoked` flag.

### 2.2 Create Unified Token Validation Service Interface (Orchestrator)

**File:** `src/Application/Identity/Interfaces/ITokenValidationService.cs`

This is the **high-level orchestrator** for post-signature token validation. It composes:
- `ISessionService` (existing) - Session validation for access tokens
- `IApiKeyService` (new) - API key revocation checks
- Permission validation - verifies user has required permissions

**Note:** JWT signature verification is handled by ASP.NET Core's JWT Bearer middleware.
This service handles **post-signature** validation (session/api-key/refresh checks).

```csharp
public enum TokenType
{
    Access,     // at+jwt
    Refresh,    // rt+jwt
    ApiKey      // ak+jwt
}

public record TokenValidationResult
{
    public bool IsValid { get; init; }
    public TokenType? Type { get; init; }
    public string? Error { get; init; }
    
    public static TokenValidationResult Success(TokenType type) 
        => new() { IsValid = true, Type = type };
    
    public static TokenValidationResult Failure(string error) 
        => new() { IsValid = false, Error = error };
}

public interface ITokenValidationService
{
    /// <summary>
    /// Validates a token AFTER signature verification by JWT middleware.
    /// Routes to appropriate validation based on typ header:
    /// - at+jwt → Session validation
    /// - ak+jwt → API key revocation check
    /// - rt+jwt → Endpoint restriction check
    /// </summary>
    /// <param name="principal">The claims principal from JWT middleware</param>
    /// <param name="securityToken">The parsed security token (to read typ header)</param>
    /// <param name="allowedTypes">Which token types are accepted for this endpoint</param>
    /// <param name="ct">Cancellation token</param>
    Task<TokenValidationResult> ValidatePostSignatureAsync(
        ClaimsPrincipal principal,
        SecurityToken securityToken,
        TokenType[] allowedTypes,
        CancellationToken ct = default);
}
```

**Service Composition:**
```
┌──────────────────────────────────────────────────────────┐
│              ITokenValidationService                      │
│                  (Orchestrator)                           │
│                                                           │
│  ┌──────────────────┐      ┌──────────────────┐          │
│  │ ISessionService  │      │ IApiKeyService   │          │
│  │ (access tokens)  │      │ (api key tokens) │          │
│  └──────────────────┘      └──────────────────┘          │
└──────────────────────────────────────────────────────────┘

Called from: ConfigureJwtBearerOptions.OnTokenValidated
```

**Usage examples:**
- Most endpoints: `ValidatePostSignatureAsync(principal, token, [TokenType.Access, TokenType.ApiKey])`
- Refresh endpoint: `ValidatePostSignatureAsync(principal, token, [TokenType.Refresh])`

### 2.3 Create API Key Service Interface

**File:** `src/Application/Identity/Interfaces/IApiKeyService.cs`

```csharp
public interface IApiKeyService
{
    /// <summary>Creates a new API key. Returns the JWT (shown once) and metadata.</summary>
    Task<(ApiKey Metadata, string Jwt)> CreateAsync(string userId, string name, DateTimeOffset? expiresAt, CancellationToken ct = default);
    
    /// <summary>Lists all non-revoked API keys for a user.</summary>
    Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    
    /// <summary>Gets a specific API key by ID.</summary>
    Task<ApiKey?> GetByIdAsync(string userId, string id, CancellationToken ct = default);
    
    /// <summary>Revokes an API key (soft delete).</summary>
    Task<bool> RevokeAsync(string userId, string id, CancellationToken ct = default);
    
    /// <summary>Validates an API key JWT. Returns metadata if valid, null if revoked/expired/not found.</summary>
    Task<ApiKey?> ValidateApiKeyAsync(string jti, CancellationToken ct = default);
    
    /// <summary>Updates LastUsedAt timestamp.</summary>
    Task UpdateLastUsedAsync(string id, CancellationToken ct = default);
    
    /// <summary>Gets count of active (non-revoked) API keys for a user.</summary>
    Task<int> GetActiveCountAsync(string userId, CancellationToken ct = default);
}
```

### 2.4 Implement Token Validation Service (Orchestrator)

**File:** `src/Application/Identity/Services/TokenValidationService.cs`

```csharp
public class TokenValidationService(
    ISessionService sessionService,
    IApiKeyService apiKeyService) : ITokenValidationService
{
    public async Task<TokenValidationResult> ValidatePostSignatureAsync(
        ClaimsPrincipal principal,
        SecurityToken securityToken,
        TokenType[] allowedTypes,
        CancellationToken ct = default)
    {
        // 1. Read typ header from security token
        var typHeader = (securityToken as JwtSecurityToken)?.Header?.Typ;
        
        // 2. Map typ header to TokenType
        var tokenType = typHeader switch
        {
            "at+jwt" => TokenType.Access,
            "rt+jwt" => TokenType.Refresh,
            "ak+jwt" => TokenType.ApiKey,
            _ => (TokenType?)null
        };
        
        if (tokenType == null)
            return TokenValidationResult.Failure("Unknown token type");
        
        // 3. Check if this token type is allowed for this endpoint
        if (!allowedTypes.Contains(tokenType.Value))
            return TokenValidationResult.Failure($"Token type {typHeader} not allowed for this endpoint");
        
        // 4. Type-specific post-validation
        switch (tokenType.Value)
        {
            case TokenType.Access:
                // Validate session is still active via ISessionService
                var sessionIdClaim = principal.FindFirst(JwtClaimTypes.SessionId);
                if (sessionIdClaim is null || !Guid.TryParse(sessionIdClaim.Value, out var sessionId))
                    return TokenValidationResult.Failure("Token is missing required session identifier");
                
                var session = await _sessionService.GetByIdAsync(sessionId, ct);
                if (session is null || !session.IsValid)
                    return TokenValidationResult.Failure("Session has been revoked or is no longer valid");
                break;
                
            case TokenType.Refresh:
                // Refresh tokens don't need additional validation here
                // The endpoint restriction is enforced by allowedTypes check above
                break;
                
            case TokenType.ApiKey:
                // Check revocation status in DB via IApiKeyService
                // jti (JWT ID, RFC 7519) is used as the API key identifier
                var apiKeyId = principal.FindFirst(JwtClaimTypes.TokenId)?.Value;
                if (string.IsNullOrEmpty(apiKeyId))
                    return TokenValidationResult.Failure("API key token is missing jti claim");
                
                var apiKey = await _apiKeyService.ValidateApiKeyAsync(apiKeyId, ct);
                if (apiKey == null)
                    return TokenValidationResult.Failure("API key has been revoked or not found");
                
                // Fire-and-forget last used update
                _ = _apiKeyService.UpdateLastUsedAsync(apiKeyId, ct);
                break;
        }
        
        return TokenValidationResult.Success(tokenType.Value);
    }
}
```

**Key Points:**
- Orchestrates existing `ISessionService` for access token session validation
- Orchestrates new `IApiKeyService` for API key revocation checks
- Type routing via `typ` header
- Single unified flow for all token types

### 2.5 Implement API Key Service

**File:** `src/Application/Identity/Services/ApiKeyService.cs`

- Inject `IApiKeyRepository`, `IUserAuthorizationService`, and `IPermissionService`
- `CreateAsync`: Generate key ID → Create JWT with `jti = keyId` → Store metadata
- `ValidateApiKeyAsync`: Check `IsRevoked == false` and `ExpiresAt` not passed
- Enforce max 100 keys per user in `CreateAsync`

### 2.6 Update ITokenProvider Interface (Required for Custom `jti`)

**File:** `src/Application/Authorization/Interfaces/Infrastructure/ITokenProvider.cs`

Add optional `tokenId` parameter to allow specifying a custom `jti` for API keys:

```csharp
Task<string> GenerateTokenWithScopesAsync(
    string userId,
    string? username,
    IEnumerable<string> scopes,
    IEnumerable<Claim>? additionalClaims = null,
    DateTimeOffset? expiration = null,
    Domain.Identity.Enums.TokenType tokenType = Domain.Identity.Enums.TokenType.Access,
    string? tokenId = null,  // NEW: Optional custom jti (used for API keys)
    CancellationToken cancellationToken = default);
```

**Rationale:** API keys need their database ID to match the JWT's `jti` claim for revocation lookup. By default, `jti` is a random GUID, but for API keys we want `jti = ApiKey.Id`.

### 2.7 Update IPermissionService Interface

**File:** `src/Application/Authorization/Interfaces/IPermissionService.cs`

Add optional `tokenId` parameter to `GenerateTokenWithScopeAsync`:

```csharp
Task<string> GenerateTokenWithScopeAsync(
    string userId,
    string? username,
    IEnumerable<ScopeDirective> scopeDirectives,
    IEnumerable<Claim>? additionalClaims = null,
    DateTimeOffset? expiration = null,
    TokenType tokenType = TokenType.Access,
    string? tokenId = null,  // NEW: Optional custom jti
    CancellationToken cancellationToken = default);
```

---

## Phase 3: Infrastructure Layer

### 3.1 Update IJwtTokenService Interface (Infrastructure Internal)

**File:** `src/Infrastructure.Identity/Interfaces/IJwtTokenService.cs`

Add optional `tokenId` parameter:

```csharp
Task<string> GenerateToken(
    string userId,
    string username,
    IEnumerable<string>? scopes = null,
    IEnumerable<Claim>? additionalClaims = null,
    DateTimeOffset? expiration = null,
    TokenType tokenType = TokenType.Access,
    string? tokenId = null,  // NEW: Optional custom jti
    CancellationToken cancellationToken = default);
```

### 3.2 Update JwtTokenService Implementation

**File:** `src/Infrastructure.Identity/Services/JwtTokenService.cs`

Update `GenerateToken` to use custom `tokenId` if provided:

```csharp
// In GenerateToken method:
var claims = new List<Claim>
{
    new(JwtClaimTypes.Subject, userId),
    new(JwtClaimTypes.Name, username),
    new(JwtClaimTypes.TokenId, tokenId ?? Guid.NewGuid().ToString()),  // Use custom or generate
    // ...
};
```

### 3.3 Update TokenProviderAdapter

**File:** `src/Infrastructure.Identity/Services/TokenProviderAdapter.cs` (or wherever `ITokenProvider` is implemented)

Pass through the `tokenId` parameter to `IJwtTokenService.GenerateToken`.

### 3.4 EF Core Configuration

**File:** `src/Infrastructure.EFCore.Identity/Configurations/ApiKeyConfiguration.cs`

- Table: `ApiKeys`
- Index on `UserId` (for listing user's keys)
- Index on `UserId, IsRevoked` (for active count)

Note: No unique index on key hash - we don't store the key, just metadata.

### 3.5 Repository Implementation

**File:** `src/Infrastructure.EFCore.Identity/Services/EFCoreApiKeyRepository.cs`

### 3.6 Add to DbContext

**File:** `src/Infrastructure.EFCore.Identity/...` (add `DbSet<ApiKeyModel>`)

### 3.7 DI Registration

Register `IApiKeyRepository` and `IApiKeyService` in service collection.

---

## Phase 4: Presentation Layer (API)

### 4.1 Folder Structure

Following the established slice-based pattern (e.g., `SessionsController/`):

```
src/Presentation.WebApi/Controllers/V1/Auth/
└── ApiKeysController/
    ├── AuthApiKeysController.cs
    ├── Requests/
    │   └── CreateApiKeyRequest.cs
    └── Responses/
        ├── ApiKeyInfoResponse.cs
        └── CreateApiKeyResponse.cs
```

### 4.2 Add Permissions

**File:** `src/Domain/Authorization/Constants/Permissions.cs`

Add under `auth` node (alongside `sessions`, `2fa`, `identity`):

```csharp
// API Keys management
Node(
    "api_keys",
    "Manage own API keys for programmatic access.",
    "API key operations",
    RLeaf("list", "List own API keys."),
    WLeaf("create", "Create a new API key."),
    WLeaf("revoke", "Revoke an API key.")
),
```

This generates:
- `PermissionIds.Api.Auth.ApiKeys.List.Identifier`
- `PermissionIds.Api.Auth.ApiKeys.Create.Identifier`
- `PermissionIds.Api.Auth.ApiKeys.Revoke.Identifier`

### 4.3 Request DTO

**File:** `Requests/CreateApiKeyRequest.cs`

```csharp
namespace Presentation.WebApi.Controllers.V1.Auth.ApiKeysController.Requests;

/// <summary>
/// Request to create a new API key.
/// </summary>
public sealed record CreateApiKeyRequest
{
    /// <summary>
    /// A friendly name for the API key (e.g., "Trading Bot", "CI/CD Pipeline").
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>
    /// Optional expiration date. If null, the key never expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
```

### 4.4 Response DTOs

**File:** `Responses/ApiKeyInfoResponse.cs`

```csharp
namespace Presentation.WebApi.Controllers.V1.Auth.ApiKeysController.Responses;

/// <summary>
/// Response containing information about an API key (without the JWT).
/// </summary>
public sealed record ApiKeyInfoResponse
{
    /// <summary>Unique identifier for the API key.</summary>
    public required string Id { get; init; }

    /// <summary>User-friendly name for the API key.</summary>
    public required string Name { get; init; }

    /// <summary>When the API key was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the API key expires (null = never).</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>When the API key was last used for authentication.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }
}
```

**File:** `Responses/CreateApiKeyResponse.cs`

```csharp
namespace Presentation.WebApi.Controllers.V1.Auth.ApiKeysController.Responses;

/// <summary>
/// Response when creating a new API key. Contains the JWT which is only shown once.
/// </summary>
public sealed record CreateApiKeyResponse
{
    /// <summary>Unique identifier for the API key.</summary>
    public required string Id { get; init; }

    /// <summary>User-friendly name for the API key.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The API key JWT. IMPORTANT: This is only returned once at creation time.
    /// Store it securely - it cannot be retrieved again.
    /// Use in Authorization header: Bearer {key}
    /// </summary>
    public required string Key { get; init; }

    /// <summary>When the API key was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the API key expires (null = never).</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
```

### 4.5 Controller

**File:** `AuthApiKeysController.cs`

```csharp
namespace Presentation.WebApi.Controllers.V1.Auth.ApiKeysController;

/// <summary>
/// Controller for managing user API keys.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/auth")]
[Produces("application/json")]
[Tags("Authentication")]
public sealed class AuthApiKeysController(IApiKeyService apiKeyService) : ControllerBase
{
    // List user's API keys
    // GET /api/v1/auth/users/{userId}/api-keys
    [HttpGet("users/{userId:guid}/api-keys")]
    [RequiredPermission(PermissionIds.Api.Auth.ApiKeys.List.Identifier)]
    [ProducesResponseType<ListResponse<ApiKeyInfoResponse>>(StatusCodes.Status200OK)]

    // Create new API key
    // POST /api/v1/auth/users/{userId}/api-keys
    [HttpPost("users/{userId:guid}/api-keys")]
    [RequiredPermission(PermissionIds.Api.Auth.ApiKeys.Create.Identifier)]
    [ProducesResponseType<CreateApiKeyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]  // Max keys reached

    // Revoke (soft-delete) an API key
    // DELETE /api/v1/auth/users/{userId}/api-keys/{id}
    [HttpDelete("users/{userId:guid}/api-keys/{id:guid}")]
    [RequiredPermission(PermissionIds.Api.Auth.ApiKeys.Revoke.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
}
```

### 4.6 Endpoint Summary

| Method | Endpoint | Permission | Description |
|--------|----------|------------|-------------|
| `GET` | `/api/v1/auth/users/{userId}/api-keys` | `api:auth:api_keys:list` | List user's API keys (excludes revoked) |
| `POST` | `/api/v1/auth/users/{userId}/api-keys` | `api:auth:api_keys:create` | Create new API key (returns secret once) |
| `DELETE` | `/api/v1/auth/users/{userId}/api-keys/{id}` | `api:auth:api_keys:revoke` | Revoke an API key |

### 4.7 Validation Rules

- **Name:** Required, 1-100 characters
- **ExpiresAt:** Optional, must be in the future if provided
- **Max keys per user:** 100 (return 400 Bad Request if exceeded)
- **Duplicate names:** Allowed (user may want multiple keys with same purpose)

### 4.8 Update ConfigureJwtBearerOptions (Infrastructure)

**File:** `src/Infrastructure.Identity/ConfigureOptions/ConfigureJwtBearerOptions.cs`

The existing `OnTokenValidated` event is replaced with unified validation via `ITokenValidationService`:

```csharp
// Add session/api-key validation on token validated
var originalOnTokenValidated = options.Events.OnTokenValidated;
options.Events.OnTokenValidated = async context =>
{
    if (originalOnTokenValidated is not null)
    {
        await originalOnTokenValidated(context);
    }

    // Skip if authentication already failed
    if (context.Result?.Failure is not null)
    {
        return;
    }

    // Use unified validation service
    using var scope = context.HttpContext.RequestServices.CreateScope();
    var tokenValidation = scope.ServiceProvider.GetRequiredService<ITokenValidationService>();
    
    // Determine allowed token types based on endpoint
    var path = context.HttpContext.Request.Path;
    var allowedTypes = GetAllowedTokenTypes(path);
    
    // Unified post-signature validation (session check, api key revocation, refresh endpoint)
    var result = await tokenValidation.ValidatePostSignatureAsync(
        context.Principal!,
        context.SecurityToken,
        allowedTypes,
        context.HttpContext.RequestAborted);
    
    if (!result.IsValid)
    {
        context.Fail(result.Error!);
    }
};

// Helper method
static TokenType[] GetAllowedTokenTypes(PathString path)
{
    // Refresh endpoint only accepts refresh tokens
    if (path.StartsWithSegments("/api/v1/auth/refresh"))
        return [TokenType.Refresh];
    
    // Most endpoints accept access tokens and API keys
    return [TokenType.Access, TokenType.ApiKey];
}
```

**Key Changes from Current Implementation:**
1. Replaces hardcoded session validation with unified `ITokenValidationService`
2. Determines allowed token types based on endpoint path
3. Validates `typ` header and routes to appropriate flow:
   - `at+jwt` → Session validation (existing `ISessionService`)
   - `ak+jwt` → API key revocation check (new `IApiKeyService`)
   - `rt+jwt` → Endpoint restriction (only `/auth/refresh`)

**What Moves to ITokenValidationService:**
```csharp
// BEFORE (in ConfigureJwtBearerOptions):
var sessionIdClaim = context.Principal?.FindFirst(JwtClaimTypes.SessionId);
var session = await sessionService.GetByIdAsync(sessionId, ct);
if (session is null || !session.IsValid)
    context.Fail("Session has been revoked");

// AFTER (in ITokenValidationService.ValidatePostSignatureAsync):
// All of this logic moves there, plus API key and refresh token handling
```

**Benefits of unified service:**
- Single entry point for all token validation
- Type-specific logic encapsulated in service
- Easy to test each token type independently
- Authentication handler stays thin

### API Key JWT Structure

The API key JWT is **identical to an access token** with two key differences:
1. `typ` header is `ak+jwt` (not `at+jwt`)
2. Uses `jti` for revocation lookup (not `sid`)

**Note:** API keys do NOT have `sid` (session ID) since they are independent of login sessions.

**Token Identification Strategy:**

| Token Type | `typ` Header | Revocation Claim | DB Lookup Key |
|------------|--------------|------------------|---------------|
| Access | `at+jwt` | `sid` (Session ID) | Session.Id |
| Refresh | `rt+jwt` | `sid` (Session ID) | Session.Id |
| API Key | `ak+jwt` | `jti` (JWT ID) | ApiKey.Id |

> **Why `jti` for API keys?** RFC 7519 defines `jti` (JWT ID) as the unique identifier for a JWT. Since API keys are independent tokens (not tied to sessions), we use `jti` directly as the database ID. Access/refresh tokens share a session, so they use `sid` instead.

**Example API Key JWT Claims:**

```json
{
  // Standard JWT claims (RFC 7519)
  "iss": "https://your-app.com",
  "aud": "https://your-app.com",
  "sub": "user-abc-123",              // userId (owner)
  "name": "john.doe@example.com",     // username
  "jti": "apikey-xyz-789",            // API key ID for revocation lookup
  "iat": 1704844800,
  "exp": 1736380800,                  // Optional (null = very far future)
  
  // RBAC version for claim resolution
  "rbac_version": "2",
  
  // Roles with inline parameters (resolved at runtime from DB)
  // Format: "ROLE_CODE;param1=value1;param2=value2"
  "roles": [
    "USER;roleUserId=user-abc-123",
    "ADMIN"
  ],
  
  // Scope contains ONLY:
  // 1. Direct permission grants (not role-derived)
  // 2. Explicit deny for api:auth:refresh
  // 3. Explicit deny for api:auth:api_keys using _read/_write wildcards
  // Role-derived scopes are resolved at runtime via IPermissionService
  "scope": [
    "allow;api:custom:feature",              // Direct grant example
    "deny;api:auth:refresh;userId=user-abc-123",
    "deny;api:auth:api_keys:_read;userId=user-abc-123",   // Denies list (RLeaf)
    "deny;api:auth:api_keys:_write;userId=user-abc-123"   // Denies create, revoke (WLeafs)
  ]
}
```

**Runtime Permission Resolution:**

Unlike static scope tokens, permission checks work as follows:
1. `RequiredPermissionAttribute` intercepts the request
2. Calls `IPermissionService.HasPermissionAsync(principal, permission)`
3. `ResolveScopeDirectivesAsync` extracts role codes from `roles` claim
4. Looks up current role definitions from database
5. Expands role scope templates with inline parameters
6. Evaluates combined scopes (direct + role-derived) against requested permission

This design allows role permission changes to take effect immediately without token regeneration.

**Key Restrictions:**

| Permission | Access Token | API Key |
|------------|--------------|---------|
| `api:auth:refresh` | ❌ Denied | ❌ Denied (explicit deny) |
| `api:auth:api_keys:list` | ✅ Yes | ❌ No (explicit deny) |
| `api:auth:api_keys:create` | ✅ Yes | ❌ No (explicit deny) |
| `api:auth:api_keys:revoke` | ✅ Yes | ❌ No (explicit deny) |
| All other permissions | ✅ Yes | ✅ Yes (mirrors user's roles) |

> **Note:** Access tokens also have an explicit deny for `api:auth:refresh` to prevent access tokens from being used for refresh operations. Only refresh tokens (`rt+jwt`) have the allow directive for refresh.

---

## Phase 5: Background Services

### 5.1 Expired API Key Cleanup Worker

**File:** `src/Infrastructure.EFCore.Identity/Workers/ExpiredApiKeyCleanupWorker.cs`

- Run daily
- Delete API keys where `ExpiresAt < now` or `RevokedAt` is old (30+ days)

---

## Phase 6: Documentation

### 6.1 Update Authentication Docs

**File:** `docs/features/authentication.md`

Add section for API key management with:
- Endpoint documentation
- Request/response examples
- Security considerations (key shown only once)

---

## Implementation Order

1. [x] **Phase 1:** Domain entity and exceptions ✅
2. [x] **Phase 2:** Application interfaces and service ✅
3. [x] **Phase 3:** Infrastructure (EF Core, repository) ✅
4. [x] **Phase 4:** API endpoints ✅
5. [ ] **Phase 5:** Background cleanup worker
6. [ ] **Phase 6:** Documentation
7. [x] **Tests:** Unit tests for service, functional tests for API ✅ (536/536 passing)

---

## Security Considerations

1. **JWT shown once:** The JWT is only returned at creation time, never again
2. **No key storage:** We don't store the JWT - only metadata (revocation check via `jti` claim)
3. **Instant revocation:** Set `IsRevoked = true` → JWT rejected on next use despite valid signature
4. **Same auth header:** Uses standard `Authorization: Bearer <jwt>` - no special handling needed
5. **Permission mirroring:** Keys inherit user's current permissions (resolved at request time)
6. **Expiration:** Optional expiration baked into JWT `exp` claim AND stored in DB for queries

---

## Design Decisions

| Question | Decision | Rationale |
|----------|----------|-----------|
| Scope restrictions? | **Yes - limited** | Same as access token EXCEPT: cannot refresh tokens (`api:auth:refresh`) and cannot manage API keys (`api:auth:api_keys:*`) |
| Max keys per user? | **100** | Prevent abuse while allowing reasonable automation use cases |
| Soft vs hard delete? | **Soft delete + cleanup** | Same pattern as sessions: `IsRevoked=true`, `RevokedAt=timestamp`, then hard delete after 30 days |
| IP allowlist? | **No** | Keep simple for v1 |
