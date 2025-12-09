# Plan: Combined Static + Dynamic Role Lookup

## Core Concept

**Roles come from two sources, merged at lookup time:**

| Source | Location | Characteristics |
|--------|----------|-----------------|
| **Static** | `Roles.cs` (Domain) | Deterministic GUIDs, immutable, always available |
| **Dynamic** | `IRoleRepository` (Database) | Random GUIDs, editable, requires Infrastructure |

**Resolution priority:** Static wins for system role IDs (prevents tampering).

**No seeding required:** Static roles are always available via `RoleLookupService` - no need to sync to DB.

---

## Architecture Changes

```
BEFORE:
  IRoleLookup (sync, Domain) ──► EFCoreRoleRepository ──► Database only
  IUserRoleResolver in Domain (but needs I/O)
  InMemoryUserStore exists in Application (redundant)

AFTER:
  IRoleLookup (async, Application) ──► RoleLookupService ──► Static (Roles.cs) + Dynamic (IRoleRepository?)
  IUserRoleResolver moved to Application (async, needs IRoleLookup)
  UserAuthenticationService (Domain) - pure password validation only, no role resolution
  InMemoryUserStore removed
```

---

## Key Design Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| Static role GUIDs | Deterministic (e.g., `00000000-...-000001`) | Consistent across installations |
| `IRoleRepository` | Optional in `RoleLookupService` | Works without database |
| `IRoleLookup` | Async, in Application | Repository access is async |
| `IUserRoleResolver` | **Moved to Application** | Needs async `IRoleLookup` - not pure Domain |
| `UserAuthenticationService` | **Stays in Domain, no role resolution** | Pure password validation only |
| Seeding static roles | **Removed** | Static roles always available, no DB sync needed |
| `InMemoryUserStore` | **Deleted** | Redundant - Infra provides real implementation |
| Service name | `RoleLookupService` | Per user preference |

---

## Domain Layer Cleanup

**`UserAuthenticationService`** becomes pure:
- Only validates password
- Records login success/failure on User entity
- Returns basic session WITHOUT permissions/roles
- No `IUserRoleResolver` dependency

**Application layer** handles role resolution:
- `IdentityService` calls `UserAuthenticationService` for password check
- Then calls `IUserRoleResolver.ResolveRolesAsync()` for roles
- Combines into final session with permissions

---

## Files to Change

| Layer | File | Action |
|-------|------|--------|
| **Domain** | `Roles.cs` | Modify - Add deterministic GUIDs, `Id` to record, static lookup methods |
| **Domain** | `IUserRoleResolver.cs` | **DELETE** - Move to Application |
| **Domain** | `UserRoleResolution.cs` | **MOVE** to Application (or keep in Domain as DTO) |
| **Domain** | `UserAuthenticationService.cs` | Modify - Remove `IUserRoleResolver`, pure password validation |
| **Application** | `IUserRoleResolver.cs` | **NEW** (moved from Domain) - Async interface |
| **Application** | `IRoleLookup.cs` | Modify - Make async |
| **Application** | `IRoleRepository.cs` | Modify - Add `GetByIdsAsync()` |
| **Application** | `RoleLookupService.cs` | **NEW** - Merges static + dynamic |
| **Application** | `UserRoleResolver.cs` | Modify - Update to async, implements new interface |
| **Application** | `RoleServiceCollectionExtensions.cs` | Modify - Register `RoleLookupService` |
| **Application** | `RoleService.cs` | Modify - Remove `EnsureSystemRolesAsync()` |
| **Application** | `IRoleService.cs` | Modify - Remove `EnsureSystemRolesAsync()` |
| **Application** | `IdentityService.cs` | Modify - Handle role resolution after auth |
| **Application** | `InMemoryUserStore.cs` | **DELETE** |
| **Infrastructure** | `EFCoreRoleRepository.cs` | Modify - Remove `IRoleLookup`, add `GetByIdsAsync()` |
| **Infrastructure** | `EFCoreIdentityServiceCollectionExtensions.cs` | Modify - Remove `IRoleLookup` registration |
| **Tests** | Various | Update to async, update stubs, remove refs |

---

## Interface Changes

```csharp
// IUserRoleResolver (Application - moved from Domain) - async
public interface IUserRoleResolver
{
    Task<IReadOnlyCollection<UserRoleResolution>> ResolveRolesAsync(User user, CancellationToken cancellationToken);
}

// IRoleLookup (Application) - make async
public interface IRoleLookup
{
    Task<Role?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Role>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
}

// IRoleRepository (Application) - add batch method
public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Role>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);  // NEW
    Task<IReadOnlyCollection<Role>> ListAsync(CancellationToken cancellationToken);
    Task SaveAsync(Role role, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

// IRoleService (Application) - remove EnsureSystemRolesAsync
public interface IRoleService
{
    Task<Role> CreateRoleAsync(RoleDescriptor descriptor, CancellationToken cancellationToken);
    Task<Role?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken);
    Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Role>> ListAsync(CancellationToken cancellationToken);  // Includes static + dynamic
    Task<Role> ReplacePermissionsAsync(Guid roleId, ...);
    Task<Role> UpdateMetadataAsync(Guid roleId, ...);
    Task<bool> DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken);
    // REMOVED: Task EnsureSystemRolesAsync(CancellationToken cancellationToken);
}
```

---

## `UserAuthenticationService` Change (Domain - Pure)

```csharp
// BEFORE: Had IUserRoleResolver, returned session with permissions
public UserAuthenticationService(
    IPasswordVerifier passwordVerifier,
    IUserRoleResolver? roleResolver = null,  // REMOVE THIS
    TimeSpan? defaultLifetime = null)

// AFTER: Pure password validation only
public sealed class UserAuthenticationService
{
    private readonly IPasswordVerifier _passwordVerifier;

    public UserAuthenticationService(IPasswordVerifier passwordVerifier)
    {
        _passwordVerifier = passwordVerifier;
    }

    /// <summary>
    /// Validates password and records login attempt.
    /// Does NOT resolve roles/permissions - that's Application layer's job.
    /// </summary>
    public void Authenticate(User user, string password, DateTimeOffset? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        var now = timestamp ?? DateTimeOffset.UtcNow;

        if (!user.CanAuthenticate(now))
            throw new AuthenticationException("User cannot authenticate");

        if (user.PasswordHash is null)
            throw new AuthenticationException("No password set");

        if (!_passwordVerifier.Verify(user.PasswordHash, password))
        {
            user.RecordFailedLogin(now);
            throw new AuthenticationException("Invalid credentials");
        }

        user.RecordSuccessfulLogin(now);
        // Note: Does NOT return session - Application layer builds that
    }
}
```

---

## `IdentityService` Change (Application - Orchestrates)

```csharp
public async Task<UserSession> AuthenticateAsync(string username, string password, CancellationToken ct)
{
    var user = await _userManager.FindByNameAsync(username)
        ?? throw new AuthenticationException("Invalid credentials");

    // 1. Domain service validates password (pure)
    _authService.Authenticate(user, password);

    // 2. Application layer resolves roles (async I/O)
    var resolvedRoles = await _roleResolver.ResolveRolesAsync(user, ct);
    var roleCodes = resolvedRoles.Select(r => r.Role.Code);
    var permissions = user.BuildEffectivePermissions(resolvedRoles);

    // 3. Save login attempt
    await _userManager.UpdateAsync(user);

    // 4. Build and return session
    return user.CreateSession(TimeSpan.FromHours(1), DateTimeOffset.UtcNow, permissions, roleCodes);
}
```

---

## Static Role GUIDs

```csharp
// Roles.cs
private static readonly Guid AdminId = new("00000000-0000-0000-0000-000000000001");
private static readonly Guid UserId  = new("00000000-0000-0000-0000-000000000002");
```

---

## `RoleLookupService` Design

```csharp
internal sealed class RoleLookupService : IRoleLookup
{
    private readonly IRoleRepository? _dynamicRepository;  // nullable - works without Infra

    public RoleLookupService(IRoleRepository? dynamicRepository = null)
    {
        _dynamicRepository = dynamicRepository;
    }

    public async Task<Role?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        // Static first (instant, no DB)
        if (Roles.TryGetById(id, out var staticRole))
            return staticRole;

        // Dynamic fallback
        return _dynamicRepository is not null
            ? await _dynamicRepository.GetByIdAsync(id, ct)
            : null;
    }

    public async Task<IReadOnlyCollection<Role>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idList = ids.ToList();
        var result = new Dictionary<Guid, Role>();
        var remainingIds = new List<Guid>();

        // Resolve static roles first
        foreach (var id in idList)
        {
            if (Roles.TryGetById(id, out var staticRole))
                result[id] = staticRole;
            else
                remainingIds.Add(id);
        }

        // Resolve remaining from dynamic
        if (remainingIds.Count > 0 && _dynamicRepository is not null)
        {
            var dynamicRoles = await _dynamicRepository.GetByIdsAsync(remainingIds, ct);
            foreach (var role in dynamicRoles)
                result[role.Id] = role;
        }

        return result.Values.ToList();
    }
}
```

---

## DI Registration Changes

**Application layer (`RoleServiceCollectionExtensions`):**
```csharp
services.AddScoped<IRoleLookup>(sp => 
    new RoleLookupService(sp.GetService<IRoleRepository>()));  // GetService = nullable

services.AddScoped<IUserRoleResolver>(sp => 
    new UserRoleResolver(sp.GetRequiredService<IRoleLookup>()));
```

**Infrastructure layer (`EFCoreIdentityServiceCollectionExtensions`):**
```csharp
// REMOVE: services.AddScoped<IRoleLookup>(...);
// KEEP:   services.AddScoped<IRoleRepository>(...);
```

---

## `RoleService.ListAsync()` Change

Should return **static + dynamic** roles merged:

```csharp
public async Task<IReadOnlyCollection<Role>> ListAsync(CancellationToken ct)
{
    var result = new Dictionary<Guid, Role>();
    
    // Add all static roles
    foreach (var role in Roles.AllRoles)
        result[role.Id] = role;
    
    // Add dynamic roles (won't override static due to same key)
    if (_repository is not null)
    {
        var dynamicRoles = await _repository.ListAsync(ct);
        foreach (var role in dynamicRoles)
        {
            if (!Roles.IsStaticRole(role.Id))  // Don't add DB copies of static roles
                result[role.Id] = role;
        }
    }
    
    return result.Values.OrderBy(r => r.Code).ToList();
}
```

---

## Files to Delete

| File | Reason |
|------|--------|
| `Application/Identity/Services/InMemoryUserStore.cs` | Redundant - Infra provides `EFCoreUserStore` |

---

## Scenarios After Implementation

| Scenario | Result |
|----------|--------|
| User assigned "ADMIN" (static ID) | Resolved from `Roles.cs`, immutable |
| User assigned "Technician" (dynamic ID) | Resolved from database |
| Admin role deleted from DB | Still works (static fallback) |
| Custom role deleted from DB | Silently ignored |
| No Infrastructure configured | Static roles still work |
| Someone edits "ADMIN" in DB | Ignored - static version wins |
| `ListAsync()` called | Returns static + dynamic merged |
| App starts fresh | No seeding needed - static roles just work |

---

## Test Updates Required

| Test File | Change |
|-----------|--------|
| `RoleServiceTests.cs` | Remove `EnsureSystemRolesAsync` tests, update to async |
| `IdentityServiceTests.cs` | Remove `EnsureSystemRolesAsync` calls, update to async |
| `UserTests.cs` | Update `StubRoleResolver` to async |
| `InMemoryRoleRepository.cs` (test fake) | Add `GetByIdsAsync()` |
