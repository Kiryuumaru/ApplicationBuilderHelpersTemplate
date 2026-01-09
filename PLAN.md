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
     → Extract keyId → DB lookup (IsRevoked? Expired?)
     → Update LastUsedAt
     
     typ: rt+jwt (Refresh Token)
     → Verify endpoint is POST /api/v1/auth/refresh
     → Reject if used on any other endpoint

2. CREATE API KEY:
   → Generate JWT with typ: ak+jwt and keyId claim
   → Store metadata in DB (id, userId, name, expiresAt)
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
    │ Validation│        │ (keyId)   │         for /refresh
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

Note: No `GetByKeyHashAsync` - we look up by `keyId` (embedded in JWT), not by hashing the key.
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
    Task<ApiKey?> ValidateApiKeyAsync(string keyId, CancellationToken ct = default);
    
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
                var keyId = principal.FindFirst(JwtClaimTypes.KeyId)?.Value;
                if (string.IsNullOrEmpty(keyId))
                    return TokenValidationResult.Failure("API key token is missing keyId claim");
                
                var apiKey = await _apiKeyService.ValidateApiKeyAsync(keyId, ct);
                if (apiKey == null)
                    return TokenValidationResult.Failure("API key has been revoked or not found");
                
                // Fire-and-forget last used update
                _ = _apiKeyService.UpdateLastUsedAsync(keyId, ct);
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

- Inject `IApiKeyRepository` and `ITokenProvider`
- `CreateAsync`: Generate key ID → Create JWT via `ITokenProvider.GenerateApiKeyTokenAsync` → Store metadata
- `ValidateApiKeyAsync`: Check `IsRevoked == false` and `ExpiresAt` not passed
- Enforce max 100 keys per user in `CreateAsync`

---

## Phase 3: Infrastructure Layer

### 3.1 EF Core Configuration

**File:** `src/Infrastructure.EFCore.Identity/Configurations/ApiKeyConfiguration.cs`

- Table: `ApiKeys`
- Index on `UserId` (for listing user's keys)
- Index on `UserId, IsRevoked` (for active count)

Note: No unique index on key hash - we don't store the key, just metadata.

### 3.2 Repository Implementation

**File:** `src/Infrastructure.EFCore.Identity/Services/EFCoreApiKeyRepository.cs`

### 3.3 Add to DbContext

**File:** `src/Infrastructure.EFCore.Identity/...` (add `DbSet<ApiKeyModel>`)

### 3.4 DI Registration

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

The API key JWT is **identical to an access token** with three differences:
1. `typ` header is `ak+jwt` (not `at+jwt`)
2. Contains `keyId` claim for revocation lookup
3. Scopes **exclude** `api:auth:refresh` and `api:auth:api_keys:*`

**Example JWT Claims:**

```json
{
  // Standard JWT claims
  "iss": "https://your-app.com",
  "aud": "https://your-app.com",
  "sub": "user-abc-123",              // userId (owner)
  "iat": 1704844800,
  "exp": 1736380800,                  // Optional (null = very far future)
  
  // Custom claims (same as access token)
  "name": "john.doe@example.com",
  "rbac_version": "2",
  "keyId": "key-xyz-789",             // API key ID for revocation lookup
  
  // Scopes - SAME as access token EXCEPT:
  // - NO "api:auth:refresh" (cannot refresh)
  // - NO "api:auth:api_keys:*" (cannot manage API keys via API key)
  "scope": [
    "allow;api:auth:me",
    "allow;api:auth:logout",
    "allow;api:auth:sessions:_read;userId=user-abc-123",
    "allow;api:iam:users:_read;userId=user-abc-123",
    // ... all user's other permissions
  ],
  
  // Roles (if user has any)
  "roles": ["admin", "trader"]
}
```

**Key Restrictions:**

| Permission | Access Token | API Key |
|------------|--------------|---------|
| `api:auth:refresh` | ✅ Yes | ❌ No (cannot refresh) |
| `api:auth:api_keys:list` | ✅ Yes | ❌ No (cannot list keys) |
| `api:auth:api_keys:create` | ✅ Yes | ❌ No (cannot create keys) |
| `api:auth:api_keys:revoke` | ✅ Yes | ❌ No (cannot revoke keys) |
| All other permissions | ✅ Yes | ✅ Yes (mirrors user) |

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

1. [ ] **Phase 1:** Domain entity and exceptions
2. [ ] **Phase 2:** Application interfaces and service
3. [ ] **Phase 3:** Infrastructure (EF Core, repository)
4. [ ] **Phase 4:** API endpoints
5. [ ] **Phase 5:** Background cleanup worker
6. [ ] **Phase 6:** Documentation
7. [ ] **Tests:** Unit tests for service, functional tests for API

---

## Security Considerations

1. **JWT shown once:** The JWT is only returned at creation time, never again
2. **No key storage:** We don't store the JWT - only metadata (revocation check via keyId claim)
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
