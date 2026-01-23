# Permission-Based Admin Access Control Plan

## Deep Code Analysis Summary

### ✅ Backend Infrastructure - ALREADY COMPLETE

After thorough analysis of the codebase, the backend permission infrastructure is **fully implemented**:

#### Domain Layer
- **`Permission.cs`** - Complete permission model with hierarchical structure (Path, Children, IsRead/IsWrite)
- **`ScopeDirective.cs`** - Allow/Deny directives with parameters (`allow;api:iam:users:_read;userId=abc`)
- **`ScopeEvaluator.cs`** - `HasPermission()`, `HasAnyPermission()`, `HasAllPermissions()` methods
- **`PermissionCache.cs`** - Cached lookups: ByPath, ReadLeafPaths, WriteLeafPaths, TreeRoots
- **`Permissions.cs`** - Static permission tree with `api.iam.users.*`, `api.iam.roles.*`, `api.iam.permissions.*`
- **`Roles.cs`** - Admin/User role definitions with ScopeTemplates

#### Application.Server Layer
- **`IPermissionService`** - `HasPermissionAsync()` for server-side checks
- **`IUserAuthorizationService`** - `GetAuthorizationDataAsync()`, `GetEffectivePermissionsAsync()`

#### Presentation.WebApp.Server Layer
- **`RequiredPermissionAttribute`** - Already used on ALL IAM controllers
- **`IamPermissionsController`** - `GET /api/v1/iam/permissions` (lists tree), `POST grant`, `POST revoke`
- **`IamRolesController`** - Full CRUD + assign/remove with `[RequiredPermission(...)]`
- **`IamUsersController`** - Full CRUD + permissions with `[RequiredPermission(...)]`

#### Application.Client Layer
- **`IPermissionsClient`** - `ListPermissionsAsync()`, `GrantPermissionAsync()`, `RevokePermissionAsync()`
- **`IRolesClient`** - Full CRUD + assign/remove
- **`IUsersClient`** - Full CRUD + `GetUserPermissionsAsync()`
- **`IamPermission`** - Client model with Children hierarchy

#### Token System
- Permissions are stored as `scope` claims in JWT tokens
- `AuthState.Permissions` is populated from JWT by `ClientAuthStateProvider`
- `BlazorAuthStateProvider` converts these to `ClaimsPrincipal` claims

### ❌ What's NOT Done - UI Layer Only

The **only** issue is that Blazor UI pages use **role-based** checks instead of **permission-based**:

| Component | Current (Wrong) | Should Be |
|-----------|-----------------|-----------|
| `Sidebar.razor` | `<AuthorizeView Roles="Admin">` | Permission-based visibility |
| `Admin/Users.razor` | `[Authorize(Roles = "Admin")]` | Permission check for `api.iam.users` |
| `Admin/Roles.razor` | `[Authorize(Roles = "Admin")]` | Permission check for `api.iam.roles` |
| `Admin/Permissions.razor` | `[Authorize(Roles = "Admin")]` | Permission check for `api.iam.permissions` |

**Also needed:**
- `PermissionTreePicker.razor` - Interactive tree for selecting permissions to grant
  - Used in: User details (direct grants), Role editor (ScopeTemplates)
  - (Note: `PermissionTreeNode.razor` exists but is display-only, no selection)

**Permission Sources (per user):**
1. **Role-inherited** - User has Role → Role has ScopeTemplates → Permissions
2. **Direct grants** - `POST /api/v1/iam/permissions/grant` assigns permission directly to user

**Permission Directives:**
- **Allow** (`allow;api:iam:users:list`) - Grants access to the permission
- **Deny** (`deny;api:iam:users:list`) - Explicitly blocks access, even if another role would allow it
- Deny takes precedence over Allow when evaluating access
- Both roles and direct grants can use Allow or Deny directives

**Constraints:**
- The "User" role cannot be removed from any user (it's the base role everyone has)

---

## Design Decisions

### Authorization Approach: Permission-Only (No Role Checks in UI)

**Chosen approach:** The UI will **only check permissions**, never roles. This ensures:
1. Consistency - All access control follows the same pattern
2. Flexibility - Admins can grant specific permissions without full role assignment
3. Separation - Roles are just "permission bundles", not a separate auth mechanism

### Component Strategy

Two custom components will handle authorization:

1. **`AppAuthorizeView`** - For conditional rendering (navigation, buttons, sections)
   - Wraps content that should only show if user has permission
   - Used in: Sidebar navigation, action buttons, admin sections

2. **`PermissionPage`** - Base component for protected pages
   - Shows `AccessDenied` component when user lacks required permission
   - Replaces `[Authorize(Roles = "Admin")]` attribute pattern

3. **`AccessDenied`** - Displayed when user navigates to page without permission
   - Friendly message explaining lack of access
   - Link to go back or request access

---

## Revised Implementation Plan

### Phase 1: Client-Side Permission Service (30 min)

Create a client-side service to evaluate permissions from `AuthState.Permissions`:

```
Application.Client/
└── Authorization/
    ├── Interfaces/
    │   └── IClientPermissionService.cs
    └── Services/
        └── ClientPermissionService.cs
```

```csharp
public interface IClientPermissionService
{
    bool HasPermission(string permissionPath);
    bool HasAnyPermission(params string[] permissionPaths);
    bool HasAllPermissions(params string[] permissionPaths);
}
```

Implementation uses `ScopeEvaluator.HasPermission()` from Domain with `AuthState.Permissions` parsed to `ScopeDirective` list.

### Phase 2: Authorization Components (1 hour)

Create reusable Blazor components:

```
Presentation.WebApp.Client/Components/Authorization/
├── AppAuthorizeView.razor      # Permission-based content visibility
├── PermissionPage.razor        # Base page with access denied handling
└── AccessDenied.razor          # Access denied UI
```

**AppAuthorizeView.razor:**
```razor
@* Shows ChildContent only if user has required permission(s) *@
<CascadingAuthenticationState>
    @if (_hasPermission)
    {
        @ChildContent
    }
    else if (NotAuthorized != null)
    {
        @NotAuthorized
    }
</CascadingAuthenticationState>

@code {
    [Parameter] public string? Permission { get; set; }
    [Parameter] public string[]? Permissions { get; set; }
    [Parameter] public bool RequireAll { get; set; } = false;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment? NotAuthorized { get; set; }
}
```

**PermissionPage.razor:**
```razor
@* Wraps page content, shows AccessDenied if lacking permission *@
@if (_hasPermission)
{
    @ChildContent
}
else
{
    <AccessDenied Permission="@Permission" />
}

@code {
    [Parameter] public string Permission { get; set; } = "";
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

### Phase 3: Update Sidebar Navigation (30 min)

Replace `<AuthorizeView Roles="Admin">` with `<AppAuthorizeView>`:

```razor
@* Before *@
<AuthorizeView Roles="Admin">
    <Authorized>
        <li>Administration</li>
        <NavLink href="admin/users">Users</NavLink>
    </Authorized>
</AuthorizeView>

@* After *@
<AppAuthorizeView Permissions="@_adminPermissions">
    <li>Administration</li>
</AppAuthorizeView>
<AppAuthorizeView Permission="@PermissionIds.Api.Iam.Users.Read.Identifier">
    <NavLink href="admin/users">Users</NavLink>
</AppAuthorizeView>
<AppAuthorizeView Permission="@PermissionIds.Api.Iam.Roles.Read.Identifier">
    <NavLink href="admin/roles">Roles</NavLink>
</AppAuthorizeView>
<AppAuthorizeView Permission="@PermissionIds.Api.Iam.Permissions.Write.Identifier">
    <NavLink href="admin/permissions">Permissions</NavLink>
</AppAuthorizeView>
```

### Phase 4: Update Admin Pages (30 min)

Replace `[Authorize(Roles = "Admin")]` with `PermissionPage` wrapper:

```razor
@* Before *@
@page "/admin/users"
@attribute [Authorize(Roles = "Admin")]

<h1>User Management</h1>
...

@* After *@
@page "/admin/users"
@attribute [Authorize]

<PermissionPage Permission="@PermissionIds.Api.Iam.Users.Read.Identifier">
    <h1>User Management</h1>
    ...
</PermissionPage>
```

Pages to update:
- `/admin/users` → `PermissionIds.Api.Iam.Users.Read.Identifier` (read scope)
- `/admin/roles` → `PermissionIds.Api.Iam.Roles.Read.Identifier` (read scope)
- `/admin/permissions` → `PermissionIds.Api.Iam.Permissions.Write.Identifier` (write scope - only has grant/revoke)

### Phase 5: Permission Tree Picker Component (2 hours)

Create `PermissionTreePicker.razor`:
- Hierarchical tree with checkboxes
- Selecting `_read`/`_write` scope selects all children
- Search/filter functionality
- `SelectedPermissions` two-way binding
- Used in role editor and user permission grant modal

### Phase 6: Permission Badge Component (30 min)

Create `PermissionBadge.razor`:
- Shows permission path with friendly formatting
- Read/Write indicator
- Optional remove button (X) for revocation

### Phase 7: Integration (1 hour)

- Add permission picker to role creation/edit modal (roles get permissions via ScopeTemplates)
- Add permission grant/revoke to user details page (users get direct permissions)
- Wire up to existing `IPermissionsClient` API calls

**Permission Assignment Model:**
- **Via Roles**: User gets role → Role has ScopeTemplates → User inherits permissions
- **Direct Grant**: User gets permission directly via `POST /permissions/grant`
- Both can be managed from the User Details page

---

## File Changes Summary

**New Files:**
```
Application.Client/Authorization/Interfaces/IClientPermissionService.cs
Application.Client/Authorization/Services/ClientPermissionService.cs
Presentation.WebApp.Client/Components/Authorization/AppAuthorizeView.razor
Presentation.WebApp.Client/Components/Authorization/PermissionPage.razor
Presentation.WebApp.Client/Components/Authorization/AccessDenied.razor
Presentation.WebApp.Client/Components/Shared/PermissionTreePicker.razor
Presentation.WebApp.Client/Components/Shared/PermissionBadge.razor
```

**Modified Files:**
```
Presentation.WebApp.Client/Layout/Sidebar.razor - Permission-based nav visibility
Presentation.WebApp.Client/Pages/Admin/Users.razor - PermissionPage wrapper + direct permission grant/revoke
Presentation.WebApp.Client/Pages/Admin/Roles.razor - PermissionPage wrapper + ScopeTemplate picker
Presentation.WebApp.Client/Pages/Admin/Permissions.razor - PermissionPage wrapper (or remove if integrated into Users)
Application.Client/ClientApplication.cs - Register IClientPermissionService
```

---

## Timeline Estimate

| Phase | Estimated Time |
|-------|----------------|
| Phase 1: Client Permission Service | 30 min |
| Phase 2: Authorization Components | 1 hour |
| Phase 3: Sidebar Navigation | 30 min |
| Phase 4: Admin Page Auth | 30 min |
| Phase 5: Permission Tree Picker | 2 hours |
| Phase 6: Permission Badge | 30 min |
| Phase 7: Integration | 1 hour |
| Phase 8: Tests | 2 hours |
| **Total** | **~8 hours** |

---

## Key Insights

1. **Backend is 100% complete** - This is a UI-only change
2. **Permission-only approach** - No role checks in UI, only permission checks
3. **`ScopeEvaluator` is reusable** - Domain layer is framework-agnostic, works client-side
4. **Leverage `PermissionIds` generated constants** - Use `.Identifier` constant for type-safe permission references
5. **Two-component pattern** - `AppAuthorizeView` for visibility, `PermissionPage` for page-level access

---

## Test Cases

### Phase 8: 100% Coverage Tests

#### Client Permission Service Tests

| Test Case | Description | Expected |
|-----------|-------------|----------|
| `HasPermission_WithAllowDirective_ReturnsTrue` | User has `allow;api:iam:users:list` | Returns true for `api:iam:users:list` |
| `HasPermission_WithDenyDirective_ReturnsFalse` | User has `deny;api:iam:users:list` | Returns false for `api:iam:users:list` |
| `HasPermission_WithNoDirective_ReturnsFalse` | User has no matching directive | Returns false |
| `HasPermission_DenyOverridesAllow` | User has both allow and deny for same permission | Returns false (deny wins) |
| `HasPermission_WithReadScope_GrantsAllReadChildren` | User has `allow;api:iam:users:_read` | Returns true for `api:iam:users:list`, `api:iam:users:read` |
| `HasPermission_WithWriteScope_GrantsAllWriteChildren` | User has `allow;api:iam:users:_write` | Returns true for `api:iam:users:update`, `api:iam:users:delete` |
| `HasPermission_WithParameterizedScope_MatchesParameter` | User has `allow;api:iam:users:read;userId=abc` | Returns true only for userId=abc |
| `HasAnyPermission_OneMatches_ReturnsTrue` | User has one of multiple permissions | Returns true |
| `HasAnyPermission_NoneMatch_ReturnsFalse` | User has none of the permissions | Returns false |
| `HasAllPermissions_AllMatch_ReturnsTrue` | User has all specified permissions | Returns true |
| `HasAllPermissions_OneMissing_ReturnsFalse` | User missing one permission | Returns false |
| `HasPermission_EmptyPermissions_ReturnsFalse` | User has no permissions at all | Returns false |
| `HasPermission_NullPermissions_ReturnsFalse` | Permissions list is null | Returns false |

#### AppAuthorizeView Component Tests

| Test Case | Description | Expected |
|-----------|-------------|----------|
| `AppAuthorizeView_WithPermission_ShowsContent` | User has required permission | Renders ChildContent |
| `AppAuthorizeView_WithoutPermission_HidesContent` | User lacks permission | Renders nothing (or NotAuthorized) |
| `AppAuthorizeView_WithNotAuthorizedTemplate_ShowsTemplate` | User lacks permission, NotAuthorized set | Renders NotAuthorized template |
| `AppAuthorizeView_WithMultiplePermissions_RequireAll_AllPresent` | RequireAll=true, user has all | Renders ChildContent |
| `AppAuthorizeView_WithMultiplePermissions_RequireAll_OneMissing` | RequireAll=true, one missing | Renders nothing |
| `AppAuthorizeView_WithMultiplePermissions_RequireAny_OnePresent` | RequireAll=false, user has one | Renders ChildContent |
| `AppAuthorizeView_Unauthenticated_HidesContent` | User not logged in | Renders nothing |

#### PermissionPage Component Tests

| Test Case | Description | Expected |
|-----------|-------------|----------|
| `PermissionPage_WithPermission_ShowsContent` | User has required permission | Renders page content |
| `PermissionPage_WithoutPermission_ShowsAccessDenied` | User lacks permission | Renders AccessDenied component |
| `PermissionPage_Unauthenticated_ShowsAccessDenied` | User not logged in | Renders AccessDenied component |

#### Sidebar Navigation Tests

| Test Case | Description | Expected |
|-----------|-------------|----------|
| `Sidebar_AdminWithAllPermissions_ShowsAllAdminLinks` | Admin user | Shows Users, Roles, Permissions links |
| `Sidebar_UserWithNoAdminPermissions_HidesAdminSection` | Regular user | Hides entire admin section |
| `Sidebar_UserWithPartialPermissions_ShowsOnlyGranted` | User with only `api:iam:users:_read` | Shows only Users link |
| `Sidebar_UserWithDenyOnUsers_HidesUsersLink` | User has deny on users | Hides Users link |

#### Admin Page Access Tests

| Test Case | Description | Expected |
|-----------|-------------|----------|
| `UsersPage_WithReadPermission_LoadsSuccessfully` | User has users read scope | Page loads, shows user list |
| `UsersPage_WithoutReadPermission_ShowsAccessDenied` | User lacks users read scope | Shows AccessDenied |
| `RolesPage_WithReadPermission_LoadsSuccessfully` | User has roles read scope | Page loads, shows role list |
| `RolesPage_WithoutReadPermission_ShowsAccessDenied` | User lacks roles read scope | Shows AccessDenied |
| `PermissionsPage_WithWritePermission_LoadsSuccessfully` | User has permissions write scope | Page loads |
| `PermissionsPage_WithoutWritePermission_ShowsAccessDenied` | User lacks permissions write scope | Shows AccessDenied |

#### Permission Tree Picker Tests

| Test Case | Description | Expected |
|-----------|-------------|----------|
| `PermissionTreePicker_LoadsFullTree` | Component initializes | Shows full permission hierarchy |
| `PermissionTreePicker_SelectLeaf_AddsToSelection` | Click leaf permission | Permission added to SelectedPermissions |
| `PermissionTreePicker_SelectReadScope_SelectsAllReadChildren` | Select `_read` scope | All read children selected |
| `PermissionTreePicker_DeselectParent_DeselectsChildren` | Deselect parent node | All children deselected |
| `PermissionTreePicker_SearchFilter_ShowsMatchingOnly` | Type in search box | Only matching permissions visible |
| `PermissionTreePicker_ToggleAllowDeny_SwitchesDirective` | Toggle allow/deny | Directive type changes |

### Chaos & Edge Case Tests

#### Permission Conflict Scenarios

| Test Case | Description | Expected |
|-----------|-------------|----------|
| `Chaos_DenyAtParent_AllowAtChild` | Deny on `api:iam:users:_read`, Allow on `api:iam:users:list` | Deny wins (deny at any level blocks) |
| `Chaos_AllowAtParent_DenyAtChild` | Allow on `api:iam:users:_read`, Deny on `api:iam:users:list` | `list` denied, other reads allowed |
| `Chaos_MultipleRolesConflict` | Role A allows, Role B denies same permission | Deny wins |
| `Chaos_DirectGrantOverridesRole` | Role allows, Direct grant denies | Deny wins |
| `Chaos_RootReadScope_DenySpecificLeaf` | Allow `_read` (root), Deny `api:iam:users:list` | All reads except users:list |

#### Malformed Data Scenarios

| Test Case | Description | Expected |
|-----------|-------------|----------|
| `Chaos_MalformedDirective_InvalidFormat` | Directive: `invalid-format` | Gracefully ignored, no crash |
| `Chaos_MalformedDirective_EmptyString` | Directive: `` | Gracefully ignored |
| `Chaos_MalformedDirective_MissingPermission` | Directive: `allow;` | Gracefully ignored |
| `Chaos_MalformedDirective_UnknownPermission` | Directive: `allow;nonexistent:permission` | No match, returns false |
| `Chaos_MalformedDirective_SqlInjection` | Directive: `allow;'; DROP TABLE users;--` | Treated as literal string, no SQL execution |
| `Chaos_MalformedDirective_XssAttempt` | Directive: `allow;<script>alert(1)</script>` | Rendered as text, no script execution |

#### Boundary & Stress Scenarios

| Test Case | Description | Expected |
|-----------|-------------|----------|
| `Chaos_UserWith1000Permissions` | User has 1000 scope directives | Still evaluates correctly, acceptable performance |
| `Chaos_DeeplyNestedPermission` | Permission 10 levels deep | Correctly resolved |
| `Chaos_ConcurrentPermissionChanges` | Permission changed while page loading | No race condition crash |
| `Chaos_TokenExpiresMidSession` | JWT expires while on admin page | Redirects to login or shows auth error |
| `Chaos_PermissionRevokedWhileOnPage` | Admin revokes own permission while on page | Next action shows access denied |

#### Role Edge Cases

| Test Case | Description | Expected |
|-----------|-------------|----------|
| `Chaos_RemoveUserRole_ShouldFail` | Attempt to remove "User" role | Operation blocked/error returned |
| `Chaos_DeleteRoleWithAssignedUsers` | Delete role that users have | Role removed from users first or blocked |
| `Chaos_CircularRoleInheritance` | Role A inherits B, B inherits A | Detected and prevented |
| `Chaos_EmptyRole_NoPermissions` | Role with zero ScopeTemplates | Valid, user just has no permissions from this role |

#### UI State Scenarios

| Test Case | Description | Expected |
|-----------|-------------|----------|
| `Chaos_RapidNavigationBetweenAdminPages` | Quick clicks between Users/Roles/Permissions | No stale state, correct permission checks |
| `Chaos_BrowserBackButton_AfterPermissionRevoked` | Navigate to page, permission revoked, press back | Shows access denied on return |
| `Chaos_RefreshPage_PermissionStillValid` | F5 refresh on admin page | Permission re-evaluated, page loads if still valid |
| `Chaos_MultipleTabsSameUser` | Open admin in two tabs, revoke in one | Other tab shows access denied on next action |
