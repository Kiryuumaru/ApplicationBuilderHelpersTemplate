# Improvement Plan: Reduce Codegen/Scaffolding Bloat (Clean Architecture)

## Goals (what “done” looks like)
- Clean Architecture dependency direction is strictly inward at the **code level**:
  - Domain has **zero** framework dependencies.
  - Application has **no** ASP.NET Core / EF Core / Identity types.
  - Infrastructure contains EF Core/Identity/WebAuthn integrations and implements Application interfaces.
  - Presentation contains HTTP concerns, endpoint contracts, and wiring only.
- Web API endpoints stop duplicating per-action `try/catch` + `ProblemDetails` boilerplate.
- AuthN/AuthZ checks are enforced consistently via **policies/requirements** rather than scattered manual checks.
- Code generation produces **smaller, more focused outputs** and does not slow down inner-loop builds.
- Shared conventions reduce controller/DTO/mapping repetition without changing behavior.

## Non-goals (explicitly out of scope)
- Rewriting to Minimal APIs, MediatR, or a new endpoint framework.
- Changing the external API surface (routes, status codes, response shapes) unless explicitly called out.
- Replacing EF Core Identity or WebAuthn library choices.
- Introducing a large “platform framework” inside the template.

## Current pain points (observed)
- Application layer currently depends on ASP.NET Core primitives (hosting, health checks) and ASP.NET Identity types.
- Controllers repeatedly build `ProblemDetails` and map exceptions inline.
- Many small request/response models and mapping helpers create high boilerplate.
- Historically: MSBuild-driven codegen ran as an `Exec` on build and generated large identifier graphs for permissions/roles.

---

## Recent changes (already completed)

- Replaced MSBuild `Exec` codegen with Roslyn incremental source generation.
- Unified source generation into a single analyzer project: `src/Domain.SourceGenerators`.
- Authorization identifier generation is gated to run only for the `Domain` compilation (analyzer is referenced by `src/Domain/Domain.csproj`).
- Build constants generation is opt-in via `GenerateBuildConstants=true` and is wired centrally in `Directory.Build.targets` (projects enabling build constants do not need per-project analyzer references).
- Renamed `Presentation.Abstractions` -> `Presentation` (project + namespaces). Base command type is now `Presentation.Commands.BaseCommand`.

**Status (as of 2026-01-07)**
- Phase 2.1 is complete and validated by `dotnet build` and `dotnet test`.
- Phase 0 (exception-to-ProblemDetails mapping + controller happy-path style) is complete and validated by `dotnet test`.
- Phase 1 / Phase 2.2 / Phase 2.3 are not started.

**Constraint**
- Logging is intentionally left as-is.
  - Do not refactor or relocate `src/Application/Logger/*` as part of this plan.

**Where to continue**
- Recommended next: Phase 1 (architecture-aligned refactors).

## Terminology (Ports & Adapters)

This plan uses **Ports & Adapters** (a.k.a. **Hexagonal Architecture**) vocabulary:

- **Inbound ports**: Application interfaces that represent use-cases (safe for Presentation to inject). Example: `IUserTokenService`.
- **Outbound ports**: Application interfaces that represent external dependencies to be implemented by Infrastructure. Examples: `ITokenProvider` (token issue/validate/decode/mutate behind one port), `IUserRepository`.
- **Driving adapters (inbound adapters)**: Presentation components that call inbound ports (e.g., API controllers).
- **Driven adapters (outbound adapters)**: Infrastructure implementations that fulfill outbound ports (e.g., JWT signer/validator, EF Core repositories).

---

## Phased plan

### Phase 0 — Quick wins (1–2 hours)

**Tasks**
- Add a single global exception-to-ProblemDetails mapping:
  - Use `UseExceptionHandler()` + `AddProblemDetails()` you already have, and register a centralized mapper (e.g., `IProblemDetailsMapper` or `IProblemDetailsService`) to translate common Domain/Application exceptions.
  - Remove per-action `try/catch` blocks in a couple of controllers as the first proof-point.
  - Treat `ApiExceptionFilter.TryMapException` as the single source of truth for **expected** failures:
    - If an exception is part of normal business validation / auth flow and should return a 4xx/409, it must be mapped in `TryMapException`.
    - Expected exceptions must be logged without stack traces (Information level).
    - Unmapped exceptions are unexpected: log at Error with full exception details and return a generic 500 `ProblemDetails`.
    - Keep match ordering strict: most-specific exception mappings first, and the generic `ValidationException` mapping last.
- Controller actions return success results only:
  - Return only “happy path” HTTP results (`Ok(...)`, `Created(...)`, `NoContent()`, etc.).
  - Express all error cases by throwing Domain/Application exceptions and let the global exception filter translate them into `ProblemDetails`.
  - Avoid constructing `ProblemDetails` (or `ValidationProblem`) in controllers for known error conditions.
- Standardize claim-type usage:
  - Replace literal claim strings (e.g., `"sub"`) with the existing constants type (`Domain.Identity.Constants.JwtClaimTypes`).
- Create small reusable response wrappers:
  - Example: `ListResponse<T>` / `PagedResponse<T>` / `IdResponse<T>` in Presentation models to cut the number of one-off response types.

**Impacted projects**
- `src/Presentation.WebApi`

**Expected payoff**
- ~30–50% less controller boilerplate in high-traffic controllers.

**Risk**
- Low (no behavior change if mapped carefully).

**Validation**
- `dotnet test`
- Run `tests/Presentation.WebApi.FunctionalTests` and ensure status codes + response payloads match.

---

### Phase 1 — Architecture-aligned refactors (1–3 days)

**Tasks**
- Remove ASP.NET Identity dependency from Application:
  - Move Identity-framework-specific behaviors (password validator usage, `UserManager<User>`, token providers) behind Application interfaces.
  - Implement those interfaces in Infrastructure (Identity/EFCore.Identity).
  - Keep Application logic expressed in terms of Domain entities/value objects + Application abstractions.
- Consolidate token services:
  - Keep a single token port boundary:
    - Keep consumer-facing orchestration at `IUserTokenService` (session creation + refresh rotation invariants).
    - Keep a single Application-owned outbound port (existing `ITokenProvider`) that covers issuing + validating + decoding + mutation.
    - Keep JWT/crypto mechanics inside Infrastructure (e.g., internal `JwtTokenService`), exposed only via the single outbound port.
  - Enforce DI boundaries via composition root (preferred) and optionally via access modifiers:
    - Presentation and other consumers may inject only **public Application interfaces** (e.g., `IUserTokenService`, `IPermissionService`).
    - Presentation consumer code must not reference or inject Infrastructure types.
    - Composition root (e.g., `Program.cs`) may reference Infrastructure for DI wiring only (prefer calling Infrastructure DI extension methods like `AddIdentityInfrastructure()` rather than constructing concrete types inline).
  - Prevent bypass paths:
    - Refresh token issuance/rotation must not be exposed as a general token API; it remains an auth/session concern owned by `IUserTokenService`.
    - Avoid alternate refresh-token formats or parallel issuance paths that bypass session validation.
- Controller conventions:
  - Introduce a single helper for `CreatedAtAction`, `NotFound`, `Conflict`, and consistent `ProblemDetails` shapes.
  - Defer mapping-layer extraction (extension methods per feature) to a later phase.

**Impacted projects**
- `src/Application`
- `src/Infrastructure.Identity`
- `src/Infrastructure.EFCore.Identity`
- `src/Presentation.WebApi`

**Affected files (controller inventory)**

These are the currently scanned controller entrypoints most impacted by “Controller conventions” (response helpers, mapping layer, repetition reduction).

- `src/Presentation.WebApi/Controllers/V1/DebugController.cs`

- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.cs`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Helpers.cs`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Identity.cs`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Login.cs`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Me.cs`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.OAuth.cs`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Passkey.cs`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Password.cs`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Sessions.cs`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.Token.cs`
- `src/Presentation.WebApi/Controllers/V1/Auth/AuthController.TwoFactor.cs`

- `src/Presentation.WebApi/Controllers/V1/Iam/IamController.cs`
- `src/Presentation.WebApi/Controllers/V1/Iam/IamController.Permissions.cs`
- `src/Presentation.WebApi/Controllers/V1/Iam/IamController.Roles.cs`
- `src/Presentation.WebApi/Controllers/V1/Iam/IamController.Users.cs`

**Target folder structure (goal state)**

Goal: controllers are grouped by feature and split by endpoint area (not by `partial` files). Endpoint-specific request/response models live with their controller area (“model dispersion”), while cross-cutting/shared API contracts remain centralized.

```
src/Presentation.WebApi/
  Controllers/
    V1/
      Auth/
        AuthHelpers.cs
        Login/
          AuthLoginController.cs
          AuthLoginHelpers.cs
          Requests/
            LoginRequest.cs
            RegisterRequest.cs
          Responses/
            AuthResponse.cs              # only if AuthResponse is truly Login-scoped
        Me/
          AuthMeController.cs
          AuthMeHelpers.cs               # only if Me-scoped helpers exist
          Responses/
            AuthMeResponse.cs            # only if Me-scoped; otherwise central Models
        Password/
          AuthPasswordController.cs
          AuthPasswordHelpers.cs         # only if Password-scoped helpers exist
          Requests/
            ForgotPasswordRequest.cs
            ResetPasswordRequest.cs
        Passkeys/
          AuthPasskeysController.cs
          AuthPasskeysHelpers.cs         # only if Passkeys-scoped helpers exist
          Requests/
            CreatePasskeyOptionsRequest.cs
            CreatePasskeyRequest.cs
        Sessions/
          AuthSessionsController.cs
          AuthSessionsHelpers.cs         # only if Sessions-scoped helpers exist
        Token/
          AuthTokenController.cs
          AuthTokenHelpers.cs            # only if Token-scoped helpers exist
        TwoFactor/
          AuthTwoFactorController.cs
          AuthTwoFactorHelpers.cs        # only if 2FA-scoped helpers exist
        OAuth/
          AuthOAuthController.cs
          AuthOAuthHelpers.cs            # only if OAuth-scoped helpers exist

      Iam/
        Users/
          IamUsersController.cs
          Requests/
            UpdateUserRequest.cs
          Responses/
            UserResponse.cs
        Roles/
          IamRolesController.cs
          Requests/
            CreateRoleRequest.cs
          Responses/
            RoleInfoResponse.cs
        Permissions/
          IamPermissionsController.cs

      Debug/
        DebugController.cs
        Requests/
          CreateAdminRequest.cs

  Models/
    Shared/
      ListResponse.cs
      PagedResponse.cs
      IdResponse.cs
      ProblemDetailsResponse.cs
```

Notes:
- Keep the URL surface unchanged by keeping the same route prefix: `api/v{v:apiVersion}/auth` and `api/v{v:apiVersion}/iam`.
- Prefer naming/namespaces that mirror folders (e.g., `Presentation.WebApi.Controllers.V1.Auth.Login`).
- Keep “shared” DTOs (used by multiple controller areas) in `src/Presentation.WebApi/Models/*` instead of duplicating them under multiple controller folders.
- Helpers:
  - Use a feature-level helper file for shared Auth helpers: `Controllers/V1/Auth/AuthHelpers.cs`.
  - For helpers used by exactly one controller area, keep them next to that area (e.g., `Controllers/V1/Auth/Login/AuthLoginHelpers.cs`).

**Expected payoff**
- Cleaner boundaries (Application becomes host-agnostic).
- Fewer indirection layers that provide no value.

**Risk**
- Medium (DI wiring changes; need good test coverage).

**Validation**
- `dotnet build`
- `dotnet test`
- Manual smoke: login/register/refresh/2FA/passkey endpoints.

---

### Phase 2 — Larger improvements (1–2 weeks)

This phase is intentionally split into smaller sub-phases to keep scope controlled and validation frequent.

#### Phase 2.1 — Incremental Roslyn source generation (build pipeline + identifiers)

**Tasks**

- Replace MSBuild `Exec` codegen with incremental Roslyn Source Generators (completed)
  - Generator assembly: `src/Domain.SourceGenerators` (targets `netstandard2.0`).
  - Authorization identifier generation emits into `Domain` only (generator is gated to the `Domain` compilation).
  - Build constants generation is opt-in via `GenerateBuildConstants=true` and is wired centrally in `Directory.Build.targets`.
    - Projects enabling build constants do not need per-project analyzer references.
    - Base command type is configured via `BaseCommandType` (defaulting to `Presentation.Commands.BaseCommand`).

**Impacted projects**
- `src/Domain.SourceGenerators`
- `src/Domain`

**Risk**
- Medium/High (build pipeline changes; generator correctness).

**Validation**
- `dotnet build` on clean checkout.
- Ensure generated sources are stable and deterministic across machines.
- `dotnet test`.

#### Phase 2.2 — Authorization policy catalog (requirements, not roles)

**Tasks**

- Add an AuthZ policy layer (policy = requirement definition, not a role)
  - Goal: move endpoint authorization requirements from scattered strings/attributes into a single, testable catalog.
  - Keep it architecture-safe:
    - Application defines policy definitions as plain types (no ASP.NET Core dependencies), e.g. `AuthorizationPolicyDefinition` records.
    - Presentation.WebApi maps those policy definitions onto ASP.NET Core authorization (filters/handlers/attributes).
  - Roles remain grants (who/what scopes you have). Policies remain requirements (what the endpoint needs).
  - Outcomes:
    - Fewer stringly-typed permission paths in controllers.
    - One place controls permission path + parameter binding rules.

**Impacted projects**
- `src/Application`
- `src/Presentation.WebApi`

**Risk**
- Medium (authorization wiring changes; requires careful tests).

**Validation**
- `dotnet test`
- Re-run `tests/Presentation.WebApi.FunctionalTests` and ensure auth behavior stays consistent.

#### Phase 2.3 — Presentation API conventions package (reduce WebApi repetition)

**Tasks**

- Add a small “API conventions” package inside Presentation (not Infrastructure)
  - Naming/location: `Presentation.WebApi.Conventions` (or `Presentation.Conventions.RestApi`).
  - Scope (high-value, low-bloat):
    - ProblemDetails conventions: stable error identifiers, titles, consistent `extensions` (e.g., `traceId`, `errorCode`).
    - Swagger/OpenAPI conventions: schema/operation filters for ProblemDetails and auth headers.
    - Standard response annotations helpers for common 400/401/403/404/409/422.
    - Binding conventions helpers (e.g., shared `[FromJwt]` patterns), kept strictly presentation-focused.
  - Non-scope:
    - No external integrations, no HTTP client code, no “SDK”.

**Impacted projects**
- `src/Presentation.WebApi`

**Risk**
- Low/Medium (mostly refactoring and consistency).

**Validation**
- `dotnet test`

**Expected payoff (Phase 2 overall)**
- Faster builds and smaller generated sources.
- More consistent authorization requirements and endpoint conventions.
- Less scaffolding drift and fewer places to update when contracts evolve.

---

## Architecture rules to enforce going forward
1. Domain must not reference framework packages (EF Core, ASP.NET Core, Identity, JSON libraries).
2. Application must not reference ASP.NET Core, EF Core, or Identity packages.
3. Application defines interfaces; Infrastructure implements them.
4. Presentation contains HTTP concerns only: routing, model binding, status code shaping.
5. Authorization evaluation logic lives in a single place (policy/handler/service), not per-controller.
6. No controller should construct `ProblemDetails` for known exceptions; use centralized mapping.
7. Avoid pass-through layers: if a service does nothing but forward, remove or give it policy.
8. Prevent accidental DI misuse:
   - Presentation injects only Application interfaces (inbound ports).
  - Presentation consumer code never references Infrastructure types; composition root may reference Infrastructure for DI wiring only.
   - Outbound ports (implemented by Infrastructure) are named by intent (`*Issuer`, `*Validator`, `*Store`) and live in a dedicated `Ports/Outbound` (or `Abstractions`) namespace.
9. Reuse shared response wrappers (`ListResponse<T>`, etc.) instead of one-off list DTOs.
10. Generated code must live in build output only (obj/analyzers), never hand-edited.
11. Codegen outputs must be deterministic and minimized.

## Codegen strategy

### Source generator build model (proposed)

- Add generator project (analyzer): `src/Domain.SourceGenerators` (targets `netstandard2.0`) (completed)
  - `Domain.csproj` references it as an analyzer (not a runtime reference).
  - Build constants generation is enabled per-project via `GenerateBuildConstants=true` and the analyzer is injected by `Directory.Build.targets`.
  - Generator reads syntax/symbols to produce deterministic, incremental outputs.

Optional: add `src/Domain.CodeGen.Abstractions` (targets `netstandard2.0`)
- Only if shared marker attributes/enums/constants are needed.
- Avoid moving any Domain behavior or logic into abstractions.

**Keep generated**
- Permission/role identifiers (stable, strongly-typed accessors).
- Build constants (version/tag/payload) if truly required by runtime.

**Make handwritten**
- Permission tree definitions and descriptions (Domain source of truth).
- Public API response DTOs (or keep tiny wrappers but do not generate thousands of DTOs).

**Reduce output size**
- Generate only what is used at compile time (nested classes + string constants).
- Move large “All paths / All params / metadata arrays” to:
  - a compact runtime manifest (JSON embedded resource), or
  - computed at startup from the Domain permission tree (if cost is acceptable).

**Contain generated code**
- Generate into compilation (source generator) or `$(IntermediateOutputPath)` only.
- Treat generated namespaces as internal implementation details (don’t expose generator-only helpers as public API).
