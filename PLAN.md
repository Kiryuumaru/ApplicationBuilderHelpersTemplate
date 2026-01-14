# UI Migration Plan: Old → New Presentation.WebApp

## Overview

This plan covers migrating the UI from the old standalone Blazor WebAssembly implementation (`src/Old/Presentation.WebApp.Client`) to the new unified Blazor Web App (`src/Presentation.WebApp` + `src/Presentation.WebApp.Client`).

### Current State

**Old Architecture (in `src/Old/`):**
- `Presentation.WebApp` - Standalone ASP.NET Core server (API only)
- `Presentation.WebApp.Client` - Standalone Blazor WebAssembly SPA
- Two separate executables, two separate deployments
- UI fully implemented with Tailwind CSS + Flowbite

**New Architecture:**
- `Presentation.WebApp` - Unified ASP.NET Core server hosting:
  - REST API endpoints (`/api/v1/*`)
  - Blazor components (server-side rendering with WebAssembly interactivity)
  - Static assets (Scalar API docs, etc.)
- `Presentation.WebApp.Client` - Blazor WebAssembly components (referenced by server)
- Single executable, single deployment
- UI is minimal/placeholder

### Goal

Migrate all UI components, pages, layouts, and styles from the old implementation to the new unified architecture while:
1. Maintaining the same user experience
2. Preserving existing functionality
3. Updating functional tests to point to the unified application

---

## Architecture Comparison

```
OLD (Two Separate Apps):                    NEW (Single Unified App):
                                            
┌─────────────────────┐                     ┌─────────────────────────────────────┐
│ Presentation.WebApp │ ← API Server        │ Presentation.WebApp                 │
│   (Port 5000)       │                     │   (Single Port: e.g., 5000)         │
└─────────────────────┘                     │                                     │
         ▲                                  │   ├── /api/v1/* → API Controllers   │
         │ HTTP                             │   ├── /scalar/* → API Docs          │
         │                                  │   ├── /* → Blazor UI (SSR + WASM)   │
┌─────────────────────┐                     │                                     │
│ Presentation.WebApp │                     │   References:                       │
│     .Client         │ ← Blazor WASM       │     Presentation.WebApp.Client      │
│   (Port 5001)       │                     │                                     │
└─────────────────────┘                     └─────────────────────────────────────┘
```

---

## Files to Migrate

### 1. Layout Components (`Layout/`)

| Old Path | New Path | Notes |
|----------|----------|-------|
| `Old/.../Layout/MainLayout.razor` | `.../Client/Layout/MainLayout.razor` | Main app shell with navbar/sidebar |
| `Old/.../Layout/AuthLayout.razor` | `.../Client/Layout/AuthLayout.razor` | Centered layout for auth pages |
| `Old/.../Layout/Navbar.razor` | `.../Client/Layout/Navbar.razor` | Top navigation bar |
| `Old/.../Layout/Sidebar.razor` | `.../Client/Layout/Sidebar.razor` | Side navigation menu |

### 2. Pages (`Pages/`)

| Old Path | New Path | Notes |
|----------|----------|-------|
| `Old/.../Pages/Home.razor` | `.../Client/Pages/Home.razor` | Dashboard home page |
| `Old/.../Pages/Auth/Login.razor` | `.../Client/Pages/Auth/Login.razor` | Login form |
| `Old/.../Pages/Auth/Register.razor` | `.../Client/Pages/Auth/Register.razor` | Registration form |
| `Old/.../Pages/Auth/ForgotPassword.razor` | `.../Client/Pages/Auth/ForgotPassword.razor` | Password reset request |
| `Old/.../Pages/Auth/ResetPassword.razor` | `.../Client/Pages/Auth/ResetPassword.razor` | Password reset form |
| `Old/.../Pages/Auth/TwoFactor.razor` | `.../Client/Pages/Auth/TwoFactor.razor` | 2FA verification |
| `Old/.../Pages/Account/Profile.razor` | `.../Client/Pages/Account/Profile.razor` | User profile |
| `Old/.../Pages/Account/ChangePassword.razor` | `.../Client/Pages/Account/ChangePassword.razor` | Change password |
| `Old/.../Pages/Account/Sessions.razor` | `.../Client/Pages/Account/Sessions.razor` | Active sessions |
| `Old/.../Pages/Account/ApiKeys.razor` | `.../Client/Pages/Account/ApiKeys.razor` | API key management |
| `Old/.../Pages/Account/TwoFactorSetup.razor` | `.../Client/Pages/Account/TwoFactorSetup.razor` | 2FA setup |
| `Old/.../Pages/Admin/Users.razor` | `.../Client/Pages/Admin/Users.razor` | User management |
| `Old/.../Pages/Admin/Roles.razor` | `.../Client/Pages/Admin/Roles.razor` | Role management |
| `Old/.../Pages/Admin/Permissions.razor` | `.../Client/Pages/Admin/Permissions.razor` | Permission management |

### 3. Components (`Components/`)

| Old Path | New Path | Notes |
|----------|----------|-------|
| `Old/.../Components/Alert.razor` | `.../Client/Components/Alert.razor` | Alert/notification component |
| `Old/.../Components/Button.razor` | `.../Client/Components/Button.razor` | Button component |
| `Old/.../Components/Card.razor` | `.../Client/Components/Card.razor` | Card container |
| `Old/.../Components/TextInput.razor` | `.../Client/Components/TextInput.razor` | Form text input |
| `Old/.../Components/LoadingSpinner.razor` | `.../Client/Components/LoadingSpinner.razor` | Loading indicator |
| `Old/.../Components/RedirectToLogin.razor` | `.../Client/Components/RedirectToLogin.razor` | Auth redirect helper |
| `Old/.../Components/PermissionTreeNode.razor` | `.../Client/Components/PermissionTreeNode.razor` | Permission tree UI |

### 4. Services (`Services/`)

| Old Path | New Path | Notes |
|----------|----------|-------|
| `Old/.../Services/BlazorAuthStateProvider.cs` | `.../Client/Services/BlazorAuthStateProvider.cs` | Already exists, verify |
| `Old/.../Services/LocalStorageTokenStorage.cs` | `.../Client/Services/...` | Token persistence |

### 5. Static Assets (`wwwroot/`)

| Old Path | New Path | Notes |
|----------|----------|-------|
| `Old/.../wwwroot/css/app.src.css` | `.../Client/wwwroot/css/app.src.css` | Tailwind source CSS |
| `Old/.../wwwroot/css/app.css` | `.../Client/wwwroot/css/app.css` | Compiled Tailwind CSS |
| `Old/.../wwwroot/index.html` | N/A | Not needed - server renders HTML |

### 6. Configuration Files

| Old Path | New Path | Notes |
|----------|----------|-------|
| `Old/.../tailwind.config.js` | `.../Client/tailwind.config.js` | Already exists, verify |
| `Old/.../package.json` | `.../Client/package.json` | Already exists, verify |

### 7. Root Files

| Old Path | New Path | Notes |
|----------|----------|-------|
| `Old/.../App.razor` | `.../Client/Routes.razor` | Update routing config |
| `Old/.../_Imports.razor` | `.../Client/_Imports.razor` | Update imports |

---

## Migration Phases

### Phase 1: Prepare Infrastructure ⬜
- [ ] Verify Tailwind CSS + Flowbite setup in new project
- [ ] Compare `package.json` and `tailwind.config.js`
- [ ] Copy/update CSS files (`app.src.css`)
- [ ] Ensure `npm run build:css` works

### Phase 2: Migrate Components ⬜
- [ ] Create `Components/` folder in Client project
- [ ] Migrate `Alert.razor`
- [ ] Migrate `Button.razor`
- [ ] Migrate `Card.razor`
- [ ] Migrate `TextInput.razor`
- [ ] Migrate `LoadingSpinner.razor`
- [ ] Migrate `RedirectToLogin.razor`
- [ ] Migrate `PermissionTreeNode.razor`

### Phase 3: Migrate Layouts ⬜
- [ ] Replace basic `MainLayout.razor` with full implementation
- [ ] Migrate `AuthLayout.razor`
- [ ] Migrate `Navbar.razor`
- [ ] Migrate `Sidebar.razor`

### Phase 4: Migrate Pages ⬜

#### 4.1: Home Page
- [ ] Replace placeholder `Home.razor` with dashboard

#### 4.2: Auth Pages
- [ ] Create `Pages/Auth/` folder
- [ ] Migrate `Login.razor`
- [ ] Migrate `Register.razor`
- [ ] Migrate `ForgotPassword.razor`
- [ ] Migrate `ResetPassword.razor`
- [ ] Migrate `TwoFactor.razor`

#### 4.3: Account Pages
- [ ] Create `Pages/Account/` folder
- [ ] Migrate `Profile.razor`
- [ ] Migrate `ChangePassword.razor`
- [ ] Migrate `Sessions.razor`
- [ ] Migrate `ApiKeys.razor`
- [ ] Migrate `TwoFactorSetup.razor`

#### 4.4: Admin Pages
- [ ] Create `Pages/Admin/` folder
- [ ] Migrate `Users.razor`
- [ ] Migrate `Roles.razor`
- [ ] Migrate `Permissions.razor`

### Phase 5: Update Routes.razor ⬜
- [ ] Update `Routes.razor` with `CascadingAuthenticationState`
- [ ] Add `AuthorizeRouteView` with proper not-authorized handling
- [ ] Add proper 404 handling

### Phase 6: Update _Imports.razor ⬜
- [ ] Add authorization usings
- [ ] Add Application.Client usings
- [ ] Add component usings

### Phase 7: Verify Services ⬜
- [ ] Compare `BlazorAuthStateProvider.cs` implementations
- [ ] Compare token storage implementations
- [ ] Ensure DI registration is complete in `Program.cs`

### Phase 8: Update Server App.razor ⬜
- [ ] Ensure proper render mode configuration
- [ ] Add CSS references for Tailwind
- [ ] Add Flowbite JS reference

---

## Test Migration

### Current Test Structure

**API Functional Tests (`Presentation.WebApp.FunctionalTests/`):**
- Tests API endpoints via HTTP
- Uses `WebApiTestHost` to start the server process
- Points to `Presentation.WebApp` (correct - no change needed)

**UI Functional Tests (`Presentation.WebApp.Client.FunctionalTests/`):**
- Tests UI via Playwright browser automation
- Currently uses TWO hosts:
  - `WebApiTestHost` - Starts API server
  - `WebAppTestHost` - Starts static file server for WASM
- **Needs update:** Single host since UI is now part of `Presentation.WebApp`

### Test Migration Tasks

#### Phase 9: Update UI Functional Tests ⬜

- [ ] Update `WebAppTestHost.cs` - No longer needed as separate static server
- [ ] Update `SharedTestFixture.cs` - Use single `WebApiTestHost` only
- [ ] Update `WebAppTestBase.cs`:
  - [ ] Change `WebAppUrl` to use same URL as `WebApiUrl`
  - [ ] Remove separate WebApp host startup
  - [ ] Update navigation helpers if paths changed
- [ ] Remove API proxy logic from `WebAppTestHost.cs` (no longer needed)
- [ ] Verify all auth flow tests still work
- [ ] Verify all component tests still work

#### Simplified Test Fixture (After Migration)

```csharp
// SharedTestFixture.cs - AFTER migration
public sealed class SharedTestFixture : IAsyncLifetime
{
    private WebApiTestHost? _host;  // Single host for everything
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public string BaseUrl => _host?.BaseUrl ?? throw new InvalidOperationException();
    public HttpClient HttpClient => _host?.HttpClient ?? throw new InvalidOperationException();

    public async Task InitializeAsync()
    {
        // Single host serves both API and UI
        _host = new WebApiTestHost(new ConsoleLogAdapter());
        await _host.StartAsync(TimeSpan.FromSeconds(30));

        // Browser setup unchanged
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(...);
    }
    
    // ... rest unchanged
}
```

---

## Key Differences to Handle

### 1. Render Mode

**Old (Pure WASM):**
```razor
<!-- index.html bootstraps WASM directly -->
<script src="_framework/blazor.webassembly.js"></script>
```

**New (Interactive WebAssembly with SSR):**
```razor
<!-- App.razor on server -->
<Routes @rendermode="new InteractiveWebAssemblyRenderMode(prerender: false)" />
<script src="@Assets["_framework/blazor.web.js"]"></script>
```

### 2. Router Configuration

**Old (App.razor):**
```razor
<Router AppAssembly="@typeof(App).Assembly">
```

**New (Routes.razor in Client project):**
```razor
<Router AppAssembly="typeof(Program).Assembly">
```

### 3. CSS Loading

**Old:**
```html
<!-- Static index.html -->
<link href="css/app.css" rel="stylesheet" />
```

**New:**
```razor
<!-- Server-rendered App.razor -->
<link rel="stylesheet" href="@Assets["app.css"]" />
```

### 4. API Base URL

**Old (Standalone WASM):**
- Configured via `appsettings.json` in WASM project
- Needed absolute URL to external API

**New (Unified):**
- API is on same origin
- Relative URLs work (`/api/v1/...`)
- No CORS needed

---

## Verification Checklist

After migration, verify:

### Build Verification
- [ ] `dotnet build` succeeds with no warnings
- [ ] `npm run build:css` in Client project succeeds
- [ ] Solution builds completely

### Runtime Verification
- [ ] Application starts successfully
- [ ] Home page loads with proper styling
- [ ] Navbar and sidebar render correctly
- [ ] Login page accessible at `/auth/login`
- [ ] Registration page accessible at `/auth/register`
- [ ] API docs accessible at `/scalar/v1`
- [ ] API endpoints work at `/api/v1/*`

### Authentication Flow
- [ ] Can register new user
- [ ] Can login with credentials
- [ ] Auth state persists across navigation
- [ ] Logout clears auth state
- [ ] Protected pages redirect to login

### Test Verification
- [ ] `dotnet test tests/Presentation.WebApp.FunctionalTests` passes
- [ ] `dotnet test tests/Presentation.WebApp.Client.FunctionalTests` passes
- [ ] All auth flow tests pass
- [ ] All UI component tests pass

---

## Notes

### Why Unified Architecture?

1. **Simpler Deployment** - Single executable, single container
2. **No CORS** - API and UI on same origin
3. **Better SEO** - Server-side rendering option
4. **Shared Resources** - Single DI container, single configuration
5. **Easier Testing** - One host to manage

### Files to Delete After Migration

Once migration is verified:
- [ ] `src/Old/` directory (entire folder)

### Breaking Changes

None expected - the UI behavior should remain identical. Only the hosting model changes.

---

## Timeline Estimate

| Phase | Estimated Time |
|-------|----------------|
| Phase 1: Infrastructure | 30 min |
| Phase 2: Components | 1 hour |
| Phase 3: Layouts | 1 hour |
| Phase 4: Pages | 2 hours |
| Phase 5-8: Configuration | 1 hour |
| Phase 9: Tests | 2 hours |
| Verification | 1 hour |
| **Total** | **~8 hours** |
