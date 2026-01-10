# WebAPI + WebApp Architecture Plan

## Overview

Create a **new** `webapi+webapp/` template alongside the existing `webapi/` template. The existing `webapi/` template remains unchanged.

**Two Templates:**
- `webapi/` - Server-only REST API template (existing, unchanged)
- `webapi+webapp/` - Combined server + client template (new)

**webapi+webapp/ contains:**
- **Server (WebAPI)**: ASP.NET Core REST API with full Identity + LocalStore EFCore
- **Client (WebApp)**: Blazor WebAssembly SPA with LocalStore-only EFCore (offline-capable)

Both share common Application/Domain layers while maintaining separate infrastructure for their specific needs.

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              SHARED LAYERS                                      │
├─────────────────────────────────────────────────────────────────────────────────┤
│  Domain                    │  Domain.SourceGenerators                           │
│  (Entities, Value Objects) │  (Code generation)                                 │
├─────────────────────────────────────────────────────────────────────────────────┤
│                           Application                                           │
│            (Common interfaces, abstractions, shared services)                   │
│                                    │                                            │
│                    ┌───────────────┴───────────────┐                            │
│                    ▼                               ▼                            │
│          Application.Server              Application.Client                     │
│          (Server-specific                (Client-specific                       │
│           abstractions)                   abstractions)                         │
└─────────────────────────────────────────────────────────────────────────────────┘
                    │                               │
                    ▼                               ▼
┌───────────────────────────────────┐ ┌───────────────────────────────────────────┐
│         SERVER STACK              │ │              CLIENT STACK                 │
├───────────────────────────────────┤ ├───────────────────────────────────────────┤
│  Presentation.WebApi              │ │  Presentation.WebApp                      │
│         │                         │ │         │                                 │
│         ▼                         │ │         ▼                                 │
│  Presentation (shared)            │ │  Presentation (shared)                    │
│         │                         │ │         │                                 │
│         ▼                         │ │         ▼                                 │
│  Infrastructure.Server.Identity   │ │  Infrastructure.EFCore.Client.Sqlite      │
│  Infrastructure.Server.Passkeys   │ │         │                                 │
│  Infrastructure.EFCore.Server.*   │ │         ▼                                 │
│         │                         │ │  Infrastructure.EFCore.Sqlite (shared)    │
│         ▼                         │ │         │                                 │
│  Infrastructure.EFCore.Sqlite     │ │         ▼                                 │
│  Infrastructure.EFCore (shared)   │ │  Infrastructure.EFCore (shared)           │
│  Infrastructure.EFCore.LocalStore │ │  Infrastructure.EFCore.LocalStore         │
└───────────────────────────────────┘ └───────────────────────────────────────────┘
```

---

## Project Structure (After Refactoring)

```
webapi+webapp/
└── src/
    │
    │  ─────────────── SHARED ───────────────
    │
    ├── Domain/                              # Entities, Value Objects
    ├── Domain.SourceGenerators/             # Code generation
    │
    ├── Application/                         # Common interfaces/abstractions
    │   ├── Abstractions/                    #   Shared service interfaces
    │   ├── Common/                          #   Shared helpers
    │   └── ...                              #   Other shared code
    │
    ├── Application.Client/                  # NEW: Client-specific abstractions
    │   └── (refs: Application)
    │
    ├── Application.Server/                  # NEW: Server-specific abstractions
    │   └── (refs: Application)
    │
    ├── Presentation/                        # Shared presentation logic
    │
    │  ─────────────── SHARED INFRASTRUCTURE ───────────────
    │
    ├── Infrastructure.EFCore/               # Base EFCore (DbContext, migrations base)
    ├── Infrastructure.EFCore.Sqlite/        # Sqlite provider (shared by client+server)
    ├── Infrastructure.EFCore.LocalStore/    # LocalStore tables (shared by client+server)
    │
    │  ─────────────── SERVER-ONLY ───────────────
    │
    ├── Presentation.WebApi/                 # ASP.NET Core REST API
    │   └── (refs: Presentation, Application.Server)
    │
    ├── Infrastructure.Server.Identity/      # RENAMED from: Infrastructure.Identity
    │   └── (refs: Application.Server)
    │
    ├── Infrastructure.Server.Passkeys/      # RENAMED from: Infrastructure.Passkeys
    │   └── (refs: Application.Server)
    │
    ├── Infrastructure.EFCore.Server.Identity/  # RENAMED from: Infrastructure.EFCore.Identity
    │   └── (refs: Infrastructure.EFCore, Application.Server)
    │
    ├── Infrastructure.EFCore.Server.Sqlite/ # NEW: Server Sqlite composition
    │   └── (refs: Infrastructure.EFCore.Sqlite, Application.Server)
    │
    │  ─────────────── CLIENT-ONLY ───────────────
    │
    ├── Presentation.WebApp/                 # NEW: Blazor WASM SPA
    │   └── (refs: Presentation, Application.Client)
    │
    └── Infrastructure.EFCore.Client.Sqlite/ # NEW: Client Sqlite (LocalStore only)
        └── (refs: Infrastructure.EFCore.Sqlite, Application.Client)
```

---

## Project Reference Graph

### Shared Projects (Platform-agnostic)

| Project | References |
|---------|------------|
| `Domain` | (none) |
| `Domain.SourceGenerators` | (Roslyn) |
| `Application` | `Domain` |
| `Presentation` | `Application` |
| `Infrastructure.EFCore` | `Application` |
| `Infrastructure.EFCore.Sqlite` | `Infrastructure.EFCore` |
| `Infrastructure.EFCore.LocalStore` | `Infrastructure.EFCore` |

### Server Projects

| Project | References |
|---------|------------|
| `Application.Server` | `Application` |
| `Infrastructure.Server.Identity` | `Application.Server` |
| `Infrastructure.Server.Passkeys` | `Application.Server` |
| `Infrastructure.EFCore.Server.Identity` | `Infrastructure.EFCore`, `Application.Server` |
| `Infrastructure.EFCore.Server.Sqlite` | `Infrastructure.EFCore.Sqlite`, `Application.Server` |
| `Presentation.WebApi` | `Presentation`, `Application.Server`, `Infrastructure.Server.Identity`, `Infrastructure.Server.Passkeys`, `Infrastructure.EFCore.Server.Identity`, `Infrastructure.EFCore.Server.Sqlite` |

### Client Projects

| Project | References |
|---------|------------|
| `Application.Client` | `Application` |
| `Infrastructure.EFCore.Client.Sqlite` | `Infrastructure.EFCore.Sqlite`, `Application.Client` |
| `Presentation.WebApp` | `Presentation`, `Application.Client`, `Infrastructure.EFCore.Client.Sqlite`, `Infrastructure.EFCore.LocalStore` |

---

## EFCore Backend Flexibility (Future Reference)

> **Note:** This section is for **future reference only**. The current implementation uses **Sqlite exclusively** for simplicity. The architecture is structured to enable this flexibility later if needed.

The architecture enables flexible database backends:

| Scenario | Server Backend | Client Backend |
|----------|---------------|----------------|
| **Current** | Sqlite | Sqlite (browser IndexedDB via sql.js) |
| Production Server (future) | PostgreSQL | Sqlite |
| Enterprise (future) | PostgreSQL | PostgreSQL |

To add PostgreSQL support in the future:
```
Infrastructure.EFCore.PostgreSQL/        # Shared PostgreSQL provider
Infrastructure.EFCore.Server.PostgreSQL/ # Server PostgreSQL composition
Infrastructure.EFCore.Client.PostgreSQL/ # Client PostgreSQL composition
```

**Current plan:** Sqlite only for both server and client.

---

## Renames (in webapi+webapp/ only)

These renames apply **only to the new `webapi+webapp/` template**. The `webapi/` template stays unchanged.

| webapi/ Name (unchanged) | webapi+webapp/ Name | Reason |
|--------------------------|---------------------|--------|
| `Infrastructure.Identity` | `Infrastructure.Server.Identity` | Server-only (JWT, auth services) |
| `Infrastructure.Passkeys` | `Infrastructure.Server.Passkeys` | Server-only (WebAuthn) |
| `Infrastructure.EFCore.Identity` | `Infrastructure.EFCore.Server.Identity` | Server-only (Identity tables) |

---

## Key Design Principles

1. **Two separate templates**: `webapi/` exists independently. `webapi+webapp/` is a new template with client+server architecture.

2. **Shared code is platform-agnostic**: `Application`, `Infrastructure.EFCore.LocalStore`, etc. must not know if they run on client or server.

3. **Client/Server split at Application layer**: `Application.Client` and `Application.Server` extend the base `Application` with platform-specific abstractions.

4. **EFCore composition via separate projects**: Each platform composes its own EFCore context by referencing the appropriate infrastructure projects.

5. **LocalStore is universal**: Both client and server use `Infrastructure.EFCore.LocalStore` for local key-value storage (settings, cache, etc.).

6. **Identity is server-only**: Authentication, authorization, and user management stay on the server. Client authenticates via API calls.

---

## Implementation Phases

### Phase 0: Preparation ✅
- [x] Create `webapi+webapp/` folder structure
- [x] Copy base projects from `webapi/` as starting point
- [x] Setup solution file

### Phase 1: Project Renames (in webapi+webapp/ only) ✅
- [x] Rename `Infrastructure.Identity` → `Infrastructure.Server.Identity`
- [x] Rename `Infrastructure.Passkeys` → `Infrastructure.Server.Passkeys`
- [x] Rename `Infrastructure.EFCore.Identity` → `Infrastructure.EFCore.Server.Identity`
- [x] Update all project references

### Phase 2: Application Layer Split ✅
- [x] Create `Application.Client` project
- [x] Create `Application.Server` project
- [x] Move server-specific abstractions from `Application` → `Application.Server`
- [x] Identify client-specific abstractions for `Application.Client`
- [x] Update references in infrastructure projects

### Phase 3: EFCore Platform Projects ✅
- [x] Create `Infrastructure.EFCore.Server.Sqlite` (composes: EFCore.Sqlite + Server.Identity + LocalStore)
- [x] Create `Infrastructure.EFCore.Client.Sqlite` (composes: EFCore.Sqlite + LocalStore only)
- [x] Ensure `Infrastructure.EFCore.LocalStore` remains platform-agnostic

### Phase 4: Presentation Layer ✅
- [x] Create `Presentation.WebApp` (Blazor WASM)
- [x] Configure client-side DI with `Application.Client`
- [x] Implement API client services for auth (calls to WebApi)

### Phase 5: Integration ✅
- [x] Wire up server with `Application.Server` + all server infrastructure
- [x] Wire up client with `Application.Client` + client infrastructure
- [x] Both templates build successfully

---

## Client (WebApp) Implementation

> **Deferred:** The WebApp frontend implementation (UI components, pages, state management) will be planned separately. This document focuses on the **client/server project separation** architecture only.

The `Presentation.WebApp` project will be a Blazor WebAssembly SPA that:
- References `Application.Client` for client-specific abstractions
- Uses `Infrastructure.EFCore.Client.Sqlite` for local storage (offline-capable)
- Calls the WebApi for authentication and server data

**Frontend implementation details (UI library, pages, state management) will be covered in a future plan.**

---

## Security Model

- **Server**: Full Identity with JWT tokens, password hashing, 2FA, passkeys
- **Client**: Stores access/refresh tokens, calls server API for auth
- **LocalStore**: Client-side cache, settings - no sensitive auth data
- **Token refresh**: Client handles 401s with silent refresh via DelegatingHandler

---

## Build & Run

```powershell
# Server
cd webapi+webapp
dotnet run --project src/Presentation.WebApi

# Client
cd webapi+webapp
dotnet run --project src/Presentation.WebApp
```

---

## Dependencies

### Server NuGet
- (existing packages from webapi/)

### Client NuGet
- Microsoft.EntityFrameworkCore.Sqlite (for WASM local storage)

---

# WebApp Frontend Implementation Plan

## Overview

This section details the full end-to-end implementation of the Blazor WebAssembly frontend, including UI components, authentication flows, state management, and API integration.

**Design Philosophy:**
- **Minimal dependencies**: Use native Blazor capabilities where possible
- **Offline-first**: LocalStore for caching, graceful degradation when offline
- **Clean architecture**: Mirror server's layered approach on client
- **Type-safe API calls**: Shared DTOs from Domain project

---

## UI Architecture

**UI Framework:** Tailwind CSS + Flowbite

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                         Presentation.WebApp                                     │
├─────────────────────────────────────────────────────────────────────────────────┤
│  Pages/                        │  Components/                                   │
│  ├── Home.razor               │  ├── Layout/                                   │
│  ├── Auth/                    │  │   ├── MainLayout.razor                      │
│  │   ├── Login.razor          │  │   ├── NavMenu.razor                         │
│  │   ├── Register.razor       │  │   ├── Sidebar.razor                         │
│  │   ├── ForgotPassword.razor │  │   └── AuthorizedLayout.razor                │
│  │   ├── ResetPassword.razor  │  ├── Flowbite/                                 │
│  │   ├── TwoFactor.razor      │  │   ├── Alert.razor                           │
│  │   └── Logout.razor         │  │   ├── Button.razor                          │
│  ├── Account/                 │  │   ├── Card.razor                            │
│  │   ├── Profile.razor        │  │   ├── Dropdown.razor                        │
│  │   ├── Security.razor       │  │   ├── Modal.razor                           │
│  │   ├── Sessions.razor       │  │   ├── Table.razor                           │
│  │   └── ApiKeys.razor        │  │   ├── TextInput.razor                       │
│  └── Admin/                   │  │   ├── Toast.razor                           │
│      ├── Users.razor          │  │   └── Spinner.razor                         │
│      ├── Roles.razor          │  ├── Auth/                                     │
│      └── Permissions.razor    │  │   ├── LoginForm.razor                       │
│                               │  │   ├── RegisterForm.razor                    │
│                               │  │   ├── PasskeyButton.razor                   │
│                               │  │   └── OAuthButtons.razor                    │
│                               │  └── Account/                                  │
│                               │      ├── ProfileForm.razor                     │
│                               │      ├── PasswordChangeForm.razor              │
│                               │      └── TwoFactorSetup.razor                  │
├─────────────────────────────────────────────────────────────────────────────────┤
│  Services/                     │  State/                                        │
│  ├── ApiClient.cs             │  ├── AuthState.cs                              │
│  ├── AuthApiClient.cs         │  ├── UserState.cs                              │
│  ├── IamApiClient.cs          │  └── AppState.cs                               │
│  └── TokenManager.cs          │                                                │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## Tailwind CSS + Flowbite Setup

### Installation

```bash
# In Presentation.WebApp directory
npm init -y
npm install -D tailwindcss postcss autoprefixer
npm install flowbite
npx tailwindcss init -p
```

### tailwind.config.js
```javascript
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './**/*.{razor,html,cshtml}',
    './wwwroot/index.html',
    './node_modules/flowbite/**/*.js'
  ],
  theme: {
    extend: {},
  },
  plugins: [
    require('flowbite/plugin')
  ],
}
```

### wwwroot/css/app.css
```css
@tailwind base;
@tailwind components;
@tailwind utilities;

/* Custom Blazor-specific styles */
.valid.modified:not([type=checkbox]) {
    @apply border-green-500;
}

.invalid {
    @apply border-red-500;
}

.validation-message {
    @apply text-red-500 text-sm mt-1;
}
```

### wwwroot/index.html
```html
<!DOCTYPE html>
<html lang="en" class="h-full bg-gray-50 dark:bg-gray-900">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>WebApp</title>
    <base href="/" />
    <link href="css/app.min.css" rel="stylesheet" />
    <link href="_framework/blazor.webassembly.js" rel="modulepreload" />
</head>
<body class="h-full">
    <div id="app">
        <!-- Loading spinner -->
        <div class="flex items-center justify-center h-screen">
            <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
        </div>
    </div>

    <script src="_framework/blazor.webassembly.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/flowbite/2.2.1/flowbite.min.js"></script>
    
    <!-- Reinitialize Flowbite after Blazor navigation -->
    <script>
        window.initFlowbite = () => {
            if (typeof initFlowbite === 'function') {
                initFlowbite();
            }
        };
    </script>
</body>
</html>
```

### Build Script (package.json)
```json
{
  "scripts": {
    "css:build": "npx tailwindcss -i ./wwwroot/css/app.css -o ./wwwroot/css/app.min.css --minify",
    "css:watch": "npx tailwindcss -i ./wwwroot/css/app.css -o ./wwwroot/css/app.min.css --watch"
  }
}
```

### MSBuild Integration (Presentation.WebApp.csproj)
```xml
<Target Name="BuildTailwind" BeforeTargets="Build" Condition="'$(Configuration)' == 'Release'">
    <Exec Command="npm run css:build" WorkingDirectory="$(ProjectDir)" />
</Target>
```

---

## Flowbite Component Wrappers

### Button.razor
```razor
@namespace Presentation.WebApp.Components.Flowbite

<button type="@Type" 
        class="@ComputedClass"
        disabled="@Disabled"
        @onclick="OnClick">
    @if (Loading)
    {
        <svg class="animate-spin -ml-1 mr-2 h-4 w-4" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
        </svg>
    }
    @ChildContent
</button>

@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Type { get; set; } = "button";
    [Parameter] public ButtonVariant Variant { get; set; } = ButtonVariant.Primary;
    [Parameter] public ButtonSize Size { get; set; } = ButtonSize.Default;
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Loading { get; set; }
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }
    [Parameter] public string? Class { get; set; }

    private string ComputedClass => $"{BaseClass} {VariantClass} {SizeClass} {Class}".Trim();
    
    private const string BaseClass = "font-medium rounded-lg focus:ring-4 focus:outline-none inline-flex items-center justify-center";
    
    private string VariantClass => Variant switch
    {
        ButtonVariant.Primary => "text-white bg-blue-700 hover:bg-blue-800 focus:ring-blue-300 dark:bg-blue-600 dark:hover:bg-blue-700 dark:focus:ring-blue-800",
        ButtonVariant.Secondary => "text-gray-900 bg-white border border-gray-300 hover:bg-gray-100 focus:ring-gray-200 dark:bg-gray-800 dark:text-white dark:border-gray-600 dark:hover:bg-gray-700 dark:focus:ring-gray-700",
        ButtonVariant.Danger => "text-white bg-red-700 hover:bg-red-800 focus:ring-red-300 dark:bg-red-600 dark:hover:bg-red-700 dark:focus:ring-red-900",
        ButtonVariant.Success => "text-white bg-green-700 hover:bg-green-800 focus:ring-green-300 dark:bg-green-600 dark:hover:bg-green-700 dark:focus:ring-green-800",
        _ => ""
    };
    
    private string SizeClass => Size switch
    {
        ButtonSize.Small => "px-3 py-2 text-xs",
        ButtonSize.Default => "px-5 py-2.5 text-sm",
        ButtonSize.Large => "px-6 py-3 text-base",
        _ => ""
    };

    public enum ButtonVariant { Primary, Secondary, Danger, Success }
    public enum ButtonSize { Small, Default, Large }
}
```

### TextInput.razor
```razor
@namespace Presentation.WebApp.Components.Flowbite

<div class="@WrapperClass">
    @if (!string.IsNullOrEmpty(Label))
    {
        <label for="@Id" class="block mb-2 text-sm font-medium text-gray-900 dark:text-white">
            @Label
            @if (Required)
            {
                <span class="text-red-500">*</span>
            }
        </label>
    }
    
    <input type="@Type"
           id="@Id"
           class="@InputClass"
           placeholder="@Placeholder"
           disabled="@Disabled"
           required="@Required"
           value="@Value"
           @oninput="HandleInput" />
    
    @if (!string.IsNullOrEmpty(HelperText))
    {
        <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">@HelperText</p>
    }
    
    @if (!string.IsNullOrEmpty(ErrorMessage))
    {
        <p class="mt-2 text-sm text-red-600 dark:text-red-500">@ErrorMessage</p>
    }
</div>

@code {
    [Parameter] public string? Id { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string Type { get; set; } = "text";
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public string? HelperText { get; set; }
    [Parameter] public string? ErrorMessage { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Required { get; set; }
    [Parameter] public string? Class { get; set; }

    private string WrapperClass => Class ?? "mb-4";
    
    private string InputClass => string.IsNullOrEmpty(ErrorMessage)
        ? "bg-gray-50 border border-gray-300 text-gray-900 text-sm rounded-lg focus:ring-blue-500 focus:border-blue-500 block w-full p-2.5 dark:bg-gray-700 dark:border-gray-600 dark:placeholder-gray-400 dark:text-white dark:focus:ring-blue-500 dark:focus:border-blue-500"
        : "bg-red-50 border border-red-500 text-red-900 placeholder-red-700 text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block w-full p-2.5 dark:bg-gray-700 dark:border-red-500 dark:placeholder-red-500 dark:text-red";

    private async Task HandleInput(ChangeEventArgs e)
    {
        await ValueChanged.InvokeAsync(e.Value?.ToString());
    }
}
```

### Alert.razor
```razor
@namespace Presentation.WebApp.Components.Flowbite

<div class="@AlertClass" role="alert">
    @if (Dismissible)
    {
        <button type="button" class="@CloseButtonClass" @onclick="Dismiss">
            <span class="sr-only">Close</span>
            <svg class="w-3 h-3" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 14 14">
                <path stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="m1 1 6 6m0 0 6 6M7 7l6-6M7 7l-6 6"/>
            </svg>
        </button>
    }
    <div class="flex items-center">
        @Icon
        <span class="sr-only">@Type</span>
        <div class="ms-3 text-sm font-medium">
            @ChildContent
        </div>
    </div>
</div>

@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public AlertType Type { get; set; } = AlertType.Info;
    [Parameter] public bool Dismissible { get; set; } = true;
    [Parameter] public EventCallback OnDismiss { get; set; }

    private async Task Dismiss() => await OnDismiss.InvokeAsync();

    private string AlertClass => Type switch
    {
        AlertType.Info => "p-4 mb-4 text-blue-800 rounded-lg bg-blue-50 dark:bg-gray-800 dark:text-blue-400",
        AlertType.Success => "p-4 mb-4 text-green-800 rounded-lg bg-green-50 dark:bg-gray-800 dark:text-green-400",
        AlertType.Warning => "p-4 mb-4 text-yellow-800 rounded-lg bg-yellow-50 dark:bg-gray-800 dark:text-yellow-300",
        AlertType.Error => "p-4 mb-4 text-red-800 rounded-lg bg-red-50 dark:bg-gray-800 dark:text-red-400",
        _ => ""
    };

    private string CloseButtonClass => Type switch
    {
        AlertType.Info => "ms-auto -mx-1.5 -my-1.5 bg-blue-50 text-blue-500 rounded-lg focus:ring-2 focus:ring-blue-400 p-1.5 hover:bg-blue-200 inline-flex items-center justify-center h-8 w-8 dark:bg-gray-800 dark:text-blue-400 dark:hover:bg-gray-700",
        AlertType.Success => "ms-auto -mx-1.5 -my-1.5 bg-green-50 text-green-500 rounded-lg focus:ring-2 focus:ring-green-400 p-1.5 hover:bg-green-200 inline-flex items-center justify-center h-8 w-8 dark:bg-gray-800 dark:text-green-400 dark:hover:bg-gray-700",
        AlertType.Warning => "ms-auto -mx-1.5 -my-1.5 bg-yellow-50 text-yellow-500 rounded-lg focus:ring-2 focus:ring-yellow-400 p-1.5 hover:bg-yellow-200 inline-flex items-center justify-center h-8 w-8 dark:bg-gray-800 dark:text-yellow-300 dark:hover:bg-gray-700",
        AlertType.Error => "ms-auto -mx-1.5 -my-1.5 bg-red-50 text-red-500 rounded-lg focus:ring-2 focus:ring-red-400 p-1.5 hover:bg-red-200 inline-flex items-center justify-center h-8 w-8 dark:bg-gray-800 dark:text-red-400 dark:hover:bg-gray-700",
        _ => ""
    };

    private RenderFragment Icon => Type switch
    {
        AlertType.Info => @<svg class="flex-shrink-0 w-4 h-4" fill="currentColor" viewBox="0 0 20 20"><path d="M10 .5a9.5 9.5 0 1 0 9.5 9.5A9.51 9.51 0 0 0 10 .5ZM9.5 4a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3ZM12 15H8a1 1 0 0 1 0-2h1v-3H8a1 1 0 0 1 0-2h2a1 1 0 0 1 1 1v4h1a1 1 0 0 1 0 2Z"/></svg>,
        AlertType.Success => @<svg class="flex-shrink-0 w-4 h-4" fill="currentColor" viewBox="0 0 20 20"><path d="M10 .5a9.5 9.5 0 1 0 9.5 9.5A9.51 9.51 0 0 0 10 .5Zm3.707 8.207-4 4a1 1 0 0 1-1.414 0l-2-2a1 1 0 0 1 1.414-1.414L9 10.586l3.293-3.293a1 1 0 0 1 1.414 1.414Z"/></svg>,
        AlertType.Warning => @<svg class="flex-shrink-0 w-4 h-4" fill="currentColor" viewBox="0 0 20 20"><path d="M10 .5a9.5 9.5 0 1 0 9.5 9.5A9.51 9.51 0 0 0 10 .5ZM10 15a1 1 0 1 1 0-2 1 1 0 0 1 0 2Zm1-4a1 1 0 0 1-2 0V6a1 1 0 0 1 2 0v5Z"/></svg>,
        AlertType.Error => @<svg class="flex-shrink-0 w-4 h-4" fill="currentColor" viewBox="0 0 20 20"><path d="M10 .5a9.5 9.5 0 1 0 9.5 9.5A9.51 9.51 0 0 0 10 .5Zm3.707 11.793a1 1 0 1 1-1.414 1.414L10 11.414l-2.293 2.293a1 1 0 0 1-1.414-1.414L8.586 10 6.293 7.707a1 1 0 0 1 1.414-1.414L10 8.586l2.293-2.293a1 1 0 0 1 1.414 1.414L11.414 10l2.293 2.293Z"/></svg>,
        _ => @<span></span>
    };

    public enum AlertType { Info, Success, Warning, Error }
}
```

---

## Application.Client Structure

```
Application.Client/
├── Authentication/
│   ├── Interfaces/
│   │   ├── IAuthStateProvider.cs       # Current auth state
│   │   ├── ITokenStorage.cs            # Token persistence
│   │   └── IApiAuthenticator.cs        # API authentication
│   ├── Models/
│   │   ├── AuthState.cs                # Current user state
│   │   ├── StoredCredentials.cs        # Persisted tokens
│   │   └── LoginResult.cs              # Login operation result
│   └── Services/
│       ├── ClientAuthStateProvider.cs  # Blazor AuthenticationStateProvider
│       ├── TokenStorageService.cs      # LocalStore token persistence
│       └── TokenRefreshHandler.cs      # DelegatingHandler for 401s
├── ApiClients/
│   ├── Interfaces/
│   │   ├── IAuthApiClient.cs           # Auth endpoints
│   │   ├── IUserApiClient.cs           # User management
│   │   └── IIamApiClient.cs            # IAM endpoints
│   └── Configuration/
│       └── ApiClientOptions.cs         # Base URL, timeout, etc.
└── Extensions/
    └── ClientServiceCollectionExtensions.cs
```

---

## Authentication Flow

### Login Flow
```
┌──────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  User    │     │  LoginPage   │     │ AuthApiClient│     │   WebApi     │
└────┬─────┘     └──────┬───────┘     └──────┬───────┘     └──────┬───────┘
     │ Enter creds      │                    │                    │
     │─────────────────>│                    │                    │
     │                  │ POST /auth/login   │                    │
     │                  │───────────────────>│                    │
     │                  │                    │ HTTP POST          │
     │                  │                    │───────────────────>│
     │                  │                    │                    │
     │                  │                    │<───────────────────│
     │                  │                    │ {accessToken,      │
     │                  │                    │  refreshToken}     │
     │                  │<───────────────────│                    │
     │                  │                    │                    │
     │                  │ Store tokens (LocalStore)               │
     │                  │─────────────────────────────────────────│
     │                  │                    │                    │
     │                  │ Update AuthState   │                    │
     │                  │─────────────────────────────────────────│
     │                  │                    │                    │
     │<─────────────────│ Navigate to home   │                    │
     │                  │                    │                    │
```

### Token Refresh Flow
```
┌──────────────┐     ┌────────────────────┐     ┌──────────────┐
│ HttpClient   │     │ TokenRefreshHandler│     │   WebApi     │
└──────┬───────┘     └─────────┬──────────┘     └──────┬───────┘
       │ API Request           │                       │
       │──────────────────────>│                       │
       │                       │ Add Bearer token      │
       │                       │──────────────────────>│
       │                       │                       │
       │                       │<──────────────────────│
       │                       │ 401 Unauthorized      │
       │                       │                       │
       │                       │ POST /auth/refresh    │
       │                       │──────────────────────>│
       │                       │                       │
       │                       │<──────────────────────│
       │                       │ {newAccessToken}      │
       │                       │                       │
       │                       │ Retry with new token  │
       │                       │──────────────────────>│
       │                       │                       │
       │<──────────────────────│ Original response     │
       │                       │                       │
```

---

## State Management

### AuthState (Singleton)
```csharp
public class AuthState
{
    public bool IsAuthenticated { get; }
    public string? UserId { get; }
    public string? Username { get; }
    public IReadOnlyList<string> Roles { get; }
    public IReadOnlyList<string> Permissions { get; }
    public DateTimeOffset? TokenExpiry { get; }
    
    public event Action? OnChange;
}
```

### Token Storage (via LocalStore)
```csharp
// Keys in LocalStore
public static class TokenStorageKeys
{
    public const string AccessToken = "auth:access_token";
    public const string RefreshToken = "auth:refresh_token";
    public const string TokenExpiry = "auth:token_expiry";
    public const string UserInfo = "auth:user_info";
}
```

---

## API Client Layer

### Base Configuration
```csharp
public class ApiClientOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; set; } = 3;
}
```

### Auth API Client Interface
```csharp
public interface IAuthApiClient
{
    // Login
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<AuthResult> LoginWithPasskeyAsync(PasskeyLoginRequest request);
    Task<AuthResult> LoginWithOAuthAsync(OAuthLoginRequest request);
    
    // Registration
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    
    // Token management
    Task<TokenRefreshResult> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync();
    
    // Password
    Task ForgotPasswordAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    Task ChangePasswordAsync(ChangePasswordRequest request);
    
    // Two-factor
    Task<TwoFactorSetupResult> SetupTwoFactorAsync();
    Task EnableTwoFactorAsync(EnableTwoFactorRequest request);
    Task DisableTwoFactorAsync(DisableTwoFactorRequest request);
    Task<AuthResult> VerifyTwoFactorAsync(TwoFactorVerifyRequest request);
    
    // Sessions
    Task<IReadOnlyList<SessionInfo>> GetSessionsAsync();
    Task RevokeSessionAsync(string sessionId);
    Task RevokeAllSessionsAsync();
    
    // Passkeys
    Task<PasskeyCreationOptions> GetPasskeyRegistrationOptionsAsync();
    Task RegisterPasskeyAsync(PasskeyRegistrationRequest request);
    Task<IReadOnlyList<PasskeyInfo>> GetPasskeysAsync();
    Task RevokePasskeyAsync(string passkeyId);
}
```

---

## Page Implementations

### Login Page Features
- Username/email + password login
- "Remember me" checkbox (extends refresh token lifetime)
- Passkey authentication (WebAuthn)
- OAuth providers (Google, Microsoft, GitHub)
- Link to registration
- Link to forgot password
- Two-factor challenge handling

### Registration Page Features
- Username, email, password fields
- Password strength indicator
- Terms of service checkbox
- Optional: OAuth registration
- Email verification flow

### Account Pages

| Page | Features |
|------|----------|
| **Profile** | View/edit username, email, display name |
| **Security** | Change password, 2FA setup, linked OAuth accounts |
| **Sessions** | View active sessions, revoke sessions |
| **API Keys** | Create/view/revoke API keys (for developers) |

### Admin Pages (Role: Admin)

| Page | Features |
|------|----------|
| **Users** | List, search, create, edit, delete users |
| **Roles** | Manage roles and their permissions |
| **Permissions** | View permission tree, assign to roles |

---

## Offline Support

### Strategy
1. **Authentication state**: Cached in LocalStore, validated on reconnect
2. **User preferences**: Stored locally, synced when online
3. **API calls**: Queue failed requests for retry when online
4. **Graceful degradation**: Show cached data with "offline" indicator

### LocalStore Usage
```csharp
// Cached data keys
public static class CacheKeys
{
    public const string UserProfile = "cache:user_profile";
    public const string UserPreferences = "cache:user_prefs";
    public const string LastSyncTime = "cache:last_sync";
}
```

---

## Component Library (Flowbite Wrappers)

### Core Components

| Component | Flowbite Base | Purpose |
|-----------|---------------|---------|
| `<FbSpinner>` | Spinner | Loading indicator |
| `<FbAlert>` | Alert | Success/error/info/warning messages |
| `<FbToast>` | Toast | Dismissible notifications |
| `<FbModal>` | Modal | Dialog/confirmation modals |
| `<FbTable>` | Table | Sortable, paginated data tables |
| `<FbCard>` | Card | Content container |
| `<FbDropdown>` | Dropdown | Dropdown menus |
| `<FbTabs>` | Tabs | Tab navigation |
| `<FbBadge>` | Badge | Status indicators |
| `<FbAvatar>` | Avatar | User avatar with fallback |

### Form Components

| Component | Flowbite Base | Purpose |
|-----------|---------------|---------|
| `<FbTextInput>` | Input | Text input with label/validation |
| `<FbPasswordInput>` | Input | Password with show/hide toggle |
| `<FbSelect>` | Select | Dropdown select |
| `<FbCheckbox>` | Checkbox | Checkbox with label |
| `<FbToggle>` | Toggle | On/off switch |
| `<FbButton>` | Button | Action buttons (variants: primary, secondary, danger) |
| `<FbFileInput>` | File Input | File upload |

### Auth Components (Custom)

| Component | Purpose |
|-----------|---------|
| `<LoginForm>` | Complete login form with validation |
| `<RegisterForm>` | Registration form |
| `<PasskeyButton>` | WebAuthn passkey authentication |
| `<OAuthButtons>` | OAuth provider buttons (Google, Microsoft, GitHub) |
| `<TwoFactorInput>` | 6-digit code input |
| `<PasswordStrength>` | Password strength meter |

### Layout Components

| Component | Purpose |
|-----------|---------|
| `<Sidebar>` | Collapsible sidebar navigation |
| `<Navbar>` | Top navigation bar |
| `<Breadcrumb>` | Page breadcrumbs |
| `<PageHeader>` | Page title with actions |

---

## Routing & Authorization

### Route Configuration
```csharp
// App.razor
<CascadingAuthenticationState>
    <Router AppAssembly="@typeof(App).Assembly">
        <Found Context="routeData">
            <AuthorizeRouteView RouteData="@routeData" 
                               DefaultLayout="@typeof(MainLayout)">
                <NotAuthorized>
                    <RedirectToLogin />
                </NotAuthorized>
            </AuthorizeRouteView>
        </Found>
        <NotFound>
            <NotFoundPage />
        </NotFound>
    </Router>
</CascadingAuthenticationState>
```

### Route Authorization
```csharp
// Public routes
@page "/login"
@page "/register"
@page "/forgot-password"

// Authenticated routes
@page "/account/profile"
@attribute [Authorize]

// Admin routes
@page "/admin/users"
@attribute [Authorize(Roles = "Admin")]

// Permission-based routes
@page "/admin/permissions"
@attribute [Authorize(Policy = "iam:permissions:read")]
```

---

## Implementation Phases

### Phase 6: Application.Client Services ✅
- [x] Create `IAuthStateProvider` and `ClientAuthStateProvider`
- [x] Create `ITokenStorage` and `TokenStorageService` (uses LocalStore)
- [x] Create `TokenRefreshHandler` (DelegatingHandler)
- [x] Create API client interfaces (`IAuthenticationClient`)
- [x] Implement API clients with HttpClient
- [x] Create auth models (AuthState, StoredCredentials, LoginResult)

### Phase 7: Tailwind + Flowbite Setup ✅
- [x] Initialize npm in Presentation.WebApp (`npm init -y`)
- [x] Install Tailwind CSS (`npm install -D tailwindcss`)
- [x] Install Flowbite (`npm install flowbite`)
- [x] Create `tailwind.config.js` with Flowbite plugin
- [x] Create `wwwroot/css/app.src.css` with Tailwind directives
- [x] Update `wwwroot/index.html` with Flowbite JS (CDN)
- [x] Add npm scripts for CSS build/watch

### Phase 8: Presentation.WebApp Infrastructure ✅
- [x] Configure DI in `Program.cs`
- [x] Setup `AuthenticationStateProvider` (BlazorAuthStateProvider)
- [x] Configure HttpClient with `TokenRefreshHandler`
- [x] Setup routing with authorization (App.razor)
- [x] Create LocalStorageTokenStorage service

### Phase 9: Flowbite Component Wrappers ✅
- [x] Create `Components/` folder
- [x] Implement `Button.razor`
- [x] Implement `TextInput.razor`
- [x] Implement `Alert.razor`
- [x] Implement `Card.razor`
- [x] Implement `LoadingSpinner.razor`
- [x] Implement `RedirectToLogin.razor`

### Phase 10: Layout Components ✅
- [x] Create `MainLayout.razor` with Flowbite sidebar
- [x] Create `Sidebar.razor` (collapsible navigation)
- [x] Create `Navbar.razor` (top bar with user menu)
- [x] Create `AuthLayout.razor` (centered card for auth pages)

### Phase 11: Authentication Pages ✅
- [x] Login page with password
- [x] Registration page
- [x] Forgot password flow
- [x] Reset password page
- [x] Two-factor verification page

### Phase 12: Account Pages ✅
- [x] Profile page (view/edit)
- [x] Change password page
- [x] Two-factor setup page

### Phase 13: Admin Pages ✅
- [x] Users list with search/pagination
- [x] Roles management page

### Phase 14: Polish & Integration
- [ ] Loading states improvements
- [ ] Error boundary components
- [ ] Responsive design verification (Tailwind breakpoints)
- [ ] Dark mode toggle implementation
- [ ] Form validation improvements
- [ ] End-to-end testing

---

## NuGet Packages (Client)

```xml
<!-- Presentation.WebApp.csproj -->
<PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" />
<PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Authentication" />
<PackageReference Include="Microsoft.Extensions.Http" />
```

## NPM Packages (Client)

```json
// package.json
{
  "devDependencies": {
    "tailwindcss": "^3.4.0",
    "postcss": "^8.4.0",
    "autoprefixer": "^10.4.0"
  },
  "dependencies": {
    "flowbite": "^2.2.1"
  }
}
```

---

## Configuration

### appsettings.json (Client)
```json
{
  "ApiClient": {
    "BaseUrl": "https://localhost:5001",
    "Timeout": "00:00:30"
  },
  "Authentication": {
    "TokenRefreshThreshold": "00:05:00"
  }
}
```

### Environment-specific
```json
// appsettings.Production.json
{
  "ApiClient": {
    "BaseUrl": "https://api.example.com"
  }
}
```

---

## Testing Strategy

### Unit Tests
- API client mocking
- State management logic
- Token refresh logic
- Offline queue logic

### Integration Tests
- Full auth flow (login → token refresh → logout)
- API client integration with real endpoints
- LocalStore persistence

### E2E Tests (Playwright)
- Login/logout flows
- Registration flow
- Admin operations
- Offline behavior
