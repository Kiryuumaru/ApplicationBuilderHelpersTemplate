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

### Phase 0: Preparation
- [ ] Create `webapi+webapp/` folder structure
- [ ] Copy base projects from `webapi/` as starting point
- [ ] Setup solution file

### Phase 1: Project Renames (in webapi+webapp/ only)
- [ ] Rename `Infrastructure.Identity` → `Infrastructure.Server.Identity`
- [ ] Rename `Infrastructure.Passkeys` → `Infrastructure.Server.Passkeys`
- [ ] Rename `Infrastructure.EFCore.Identity` → `Infrastructure.EFCore.Server.Identity`
- [ ] Update all project references

### Phase 2: Application Layer Split
- [ ] Create `Application.Client` project
- [ ] Create `Application.Server` project
- [ ] Move server-specific abstractions from `Application` → `Application.Server`
- [ ] Identify client-specific abstractions for `Application.Client`
- [ ] Update references in infrastructure projects

### Phase 3: EFCore Platform Projects
- [ ] Create `Infrastructure.EFCore.Server.Sqlite` (composes: EFCore.Sqlite + Server.Identity + LocalStore)
- [ ] Create `Infrastructure.EFCore.Client.Sqlite` (composes: EFCore.Sqlite + LocalStore only)
- [ ] Ensure `Infrastructure.EFCore.LocalStore` remains platform-agnostic

### Phase 4: Presentation Layer
- [ ] Create `Presentation.WebApp` (Blazor WASM)
- [ ] Configure client-side DI with `Application.Client` + `Infrastructure.EFCore.Client.Sqlite`
- [ ] Implement API client services for auth (calls to WebApi)

### Phase 5: Integration
- [ ] Wire up server with `Application.Server` + all server infrastructure
- [ ] Wire up client with `Application.Client` + client infrastructure
- [ ] Test both run independently
- [ ] Test client-server communication

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
