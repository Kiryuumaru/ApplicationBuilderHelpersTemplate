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
- MSBuild-driven codegen runs as an `Exec` on build and generates large identifier graphs for permissions/roles.

## Terminology (Ports & Adapters)

This plan uses **Ports & Adapters** (a.k.a. **Hexagonal Architecture**) vocabulary:

- **Inbound ports**: Application interfaces that represent use-cases (safe for Presentation to inject). Example: `IUserTokenService`.
- **Outbound ports**: Application interfaces that represent external dependencies to be implemented by Infrastructure. Examples: `ITokenIssuer`, `ITokenValidator`, `IUserRepository`.
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
- Remove ASP.NET hosting types from Application:
  - Move health-check endpoint mapping and middleware configuration out of `Application.Application` into Presentation.
  - Keep Application responsible only for registering application services.
  - Exception (explicit): keep logging services in Application.
    - `Application.Logger` stays responsible for configuring Serilog/OpenTelemetry and registering logger-related services.
    - Presentation may call Application logging extension methods, but we do not remove/migrate the logging subsystem out of Application.
- Consolidate token services:
  - Apply a clear token-port split (less friction than friend assemblies):
    - Keep consumer-facing orchestration at `IUserTokenService` (session creation + refresh rotation invariants).
    - Replace generic “provider” naming with intent-revealing ports implemented by Infrastructure:
      - `ITokenIssuer` (issue signed tokens from claims/scopes)
      - `ITokenValidator` (validate/decode/mutate tokens)
      - Alternatively, a single `ITokenCodec` if you want one boundary.
    - Place ports under a dedicated folder/namespace to make intent obvious:
      - `Application/Authorization/Ports/Outbound/*` (implemented by Infrastructure)
      - `Application/Identity/Ports/Inbound/*` (used by Presentation)
    - Keep JWT/crypto mechanics inside Infrastructure (e.g., internal `JwtTokenService`), exposed only via the outbound ports.
  - Enforce DI boundaries via composition root (preferred) and optionally via access modifiers:
    - Presentation and other consumers may inject only **public Application interfaces** (e.g., `IUserTokenService`, `IPermissionService`).
    - Infrastructure registrations occur only in Infrastructure DI extension methods (e.g., `AddIdentityInfrastructure()`), keeping concrete types out of Presentation.
    - Optionally keep outbound port implementations `internal` to Infrastructure to reduce accidental injection even when referenced.
  - Prevent bypass paths:
    - Refresh token issuance/rotation must not be exposed as a general token API; it remains an auth/session concern owned by `IUserTokenService`.
    - Avoid alternate refresh-token formats or parallel issuance paths that bypass session validation.
- Controller conventions:
  - Introduce a single helper for `CreatedAtAction`, `NotFound`, `Conflict`, and consistent `ProblemDetails` shapes.
  - Replace repeated mapping helpers with a small mapping layer (extension methods per feature).

**Impacted projects**
- `src/Application`
- `src/Infrastructure.Identity`
- `src/Infrastructure.EFCore.Identity`
- `src/Presentation.WebApi`

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

**Tasks**
- Replace MSBuild `Exec` codegen with an incremental Roslyn Source Generator (or tighten MSBuild incremental inputs/outputs):
  - Generate `PermissionIds`/`RoleIds` as compilation-time generated sources without spawning `dotnet`.
  - Generate smaller outputs: focus on identifiers needed by code, move large flat lists (All paths, metadata arrays) to a compact embedded resource or JSON manifest.
- Add an AuthZ policy layer:
  - Replace (or re-implement) `RequiredPermissionAttribute` as a policy/requirement that plugs into ASP.NET Core authorization.
  - Ensure one place is responsible for permission evaluation and parameter binding.
- Add a small “API conventions” package inside Presentation:
  - Standardized ProblemDetails types, error codes, and mapping.

**Impacted projects**
- `src/Domain.CodeGenerator` (replaced/repurposed)
- `src/Domain`
- `src/Presentation.WebApi`

**Expected payoff**
- Faster builds and smaller generated sources.
- Less scaffolding drift and fewer places to update when contracts evolve.

**Risk**
- Medium/High (build pipeline changes; generator correctness).

**Validation**
- `dotnet build` on clean checkout.
- Ensure generated sources are stable and deterministic across machines.
- `dotnet test`.

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
   - Infrastructure is registered via DI extension methods; Presentation never references Infrastructure token types.
   - Outbound ports (implemented by Infrastructure) are named by intent (`*Issuer`, `*Validator`, `*Store`) and live in a dedicated `Ports/Outbound` (or `Abstractions`) namespace.
9. Reuse shared response wrappers (`ListResponse<T>`, etc.) instead of one-off list DTOs.
10. Generated code must live in build output only (obj/analyzers), never hand-edited.
11. Codegen outputs must be deterministic and minimized.

## Codegen strategy

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
