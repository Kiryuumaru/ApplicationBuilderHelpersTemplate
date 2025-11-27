# Plan: Raw SQLite & Custom Identity Stores

This plan focuses on maintaining a lightweight, dependency-free architecture by using raw SQLite (ADO.NET) instead of Entity Framework Core. We will implement Microsoft Identity interfaces manually to support standard authentication features without the overhead of an ORM.

**Core Principle: Persistence Ignorance**
The Domain and Application layers will remain completely agnostic of the underlying database technology. All data access will be performed through interfaces defined in the Application layer (e.g., `IUserStore`, `IRoleStore`, `ILocalStoreService`), implemented in separate Infrastructure projects. This ensures that the SQLite implementation can be swapped for PostgreSQL, SQL Server, or any other provider without modifying the core business logic.

## 1. Domain Refactoring (Identity Compatible)
- [x] **Refactor `Domain.Shared`**:
    - **Goal**: Update base classes to support generic strongly typed IDs and introduce a common root.
    - **Actions**:
        - Create `DomainObject` base class (parent for Entity, ValueObject).
        - **Add common logic to `DomainObject` (e.g., validation helpers, cloning, or reflection utilities) that applies to all domain objects.**
        - Update `Entity` to `Entity<TId>` inheriting from `DomainObject`.
        - **Enforce ID initialization in `Entity<TId>` constructor to ensure null safety (avoid `default!`).**
        - Ensure `ValueObject` inherits from `DomainObject`.
        - Ensure `RevId` is regenerated on modification (similar to `LastModified` in `AuditableEntity`).
        - Add `RevId` to `Entity` constructor as optional parameter (next to `Id`).
- [x] **Refactor `User` Entity**:
    - **Strategy**: Keep as pure Domain Entity (No inheritance from `IdentityUser`).
    - **Implementation**: Manually add all standard Identity fields (`PasswordHash`, `SecurityStamp`, `NormalizedEmail`, etc.) to the `User` class.
    - **Reasoning**: This avoids polluting the Domain with `Microsoft.Extensions.Identity.Stores` dependencies while ensuring the `CustomUserStore` can map 1:1 to the Domain entity without complex translation layers.
    - **Actions**:
        - Remove `PasswordCredential` ValueObject.
        - Add: `UserId Id` (Strongly Typed), `UserName`, `NormalizedUserName`, `Email`, `NormalizedEmail`, `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `PhoneNumber`, `TwoFactorEnabled`, `LockoutEnd`, `LockoutEnabled`, `AccessFailedCount`.
    - Keep `PermissionGrants` and `RoleAssignments` (custom logic).
- [x] **Refactor `Role` Entity**:
    - Match Identity structure (`RoleId Id`, `Name`, `NormalizedName`, `ConcurrencyStamp`).
- [x] **Maintain Code Generators**:
    - Ensure `Domain.CodeGenerator` (PermissionIds, RoleIds) continues to work.
    - *Note*: `RoleIdsGenerator` reads from `Roles.All`. We must ensure `Role` changes don't break the static definitions.

## 2. Architecture Restructuring (DDD & DI)
- [x] **Create `src/Infrastructure.Sqlite` Project**:
    - Target `net10.0`.
    - Add references: `Microsoft.Data.Sqlite`.
    - Depends on: `src/Application`.
    - Contains:
        - `SqliteConnectionFactory`: Helper to manage connections.
        - `DatabaseBootstrap`: Base logic/interface for table creation.
        - `DatabaseInitializationState`: Tracks when database is ready for queries.
        - `SqliteDatabaseBootstrapperWorker`: Hosted service that runs table initialization on startup.
        - *Note*: Must handle Type Handlers for Strongly Typed IDs (`UserId`, `RoleId`) manually in ADO.NET.
    - **Folder Structure**:
        - Root: `SqliteInfrastructure.cs` (ApplicationDependency)
        - `Extensions/`: Internal service collection extensions
        - `Interfaces/`: `IDatabaseBootstrap`
        - `Services/`: `SqliteConnectionFactory`, `DatabaseBootstrap`, `DatabaseInitializationState`
        - `Workers/`: `SqliteDatabaseBootstrapperWorker`

- [x] **Create `src/Infrastructure.Sqlite.LocalStore` Project**:
    - Target `net10.0`.
    - Depends on: `src/Infrastructure.Sqlite`, `src/Application` (for `ILocalStoreService`).
    - Contains:
        - `SqliteLocalStoreService`: Implementation (waits for database initialization before opening connections).
        - `LocalStoreTableInitializer`: Script to create LocalStore table.
    - **Folder Structure**:
        - Root: `SqliteLocalStoreInfrastructure.cs`
        - `Extensions/`: Internal service collection extensions
        - `Services/`: `SqliteLocalStoreService`, `LocalStoreTableInitializer`

- [x] **Create `src/Infrastructure.Sqlite.Identity` Project**:
    - Target `net10.0`.
    - Add references: `Microsoft.Extensions.Identity.Core`.
    - Depends on: `src/Infrastructure.Sqlite`, `src/Application`, `src/Domain`.
    - Contains:
        - `CustomUserStore`: Implementation of Identity interfaces.
        - `CustomRoleStore`: Implementation of Identity interfaces.
        - `SqliteRoleRepository`: Repository for role management.
        - `IdentityTableInitializer`: Script to create Users, Roles, UserRoles, UserClaims, etc. tables.
        - *Note*: Stores must convert `UserId` <-> `string/Guid` when talking to Identity interfaces (which often expect strings or Guids).
    - **Folder Structure**:
        - Root: `SqliteIdentityInfrastructure.cs`
        - `Extensions/`: Internal service collection extensions
        - `Services/`: `CustomUserStore`, `CustomRoleStore`, `SqliteRoleRepository`, `IdentityTableInitializer`

- [ ] **Create `src/Infrastructure.Sqlite.Authorization` Project** (Optional/Future):
    - Target `net10.0`.
    - Depends on: `src/Infrastructure.Sqlite`, `src/Domain`.
    - Contains: Persistence for custom permission grants if not handled by Identity Claims.

## 3. Infrastructure Implementation (The "Hard" Part)
- [x] **Implement `CustomUserStore` (in `Infrastructure.Sqlite.Identity`)**:
    - Implement interfaces: `IUserStore<User>`, `IUserPasswordStore<User>`, `IUserEmailStore<User>`, `IUserRoleStore<User>`, `IUserSecurityStampStore<User>`, `IUserLockoutStore<User>`, `IUserPhoneNumberStore<User>`, `IUserTwoFactorStore<User>`, `IUserLoginStore<User>`.
    - Write raw SQL for CRUD operations.
    - **Crucial**: Handle `UserId` conversion in SQL parameters.
- [x] **Implement `CustomRoleStore` (in `Infrastructure.Sqlite.Identity`)**:
    - Implement `IRoleStore<Role>`.
    - Write raw SQL for CRUD operations.
    - **Crucial**: Handle `RoleId` conversion.
- [x] **Implement `SqliteLocalStoreService` (in `Infrastructure.Sqlite.LocalStore`)**:
    - Move existing logic from `src/Application/LocalStore` to this new project.
    - Adapt to use `SqliteConnectionFactory`.
    - **Added**: Wait for database initialization before opening connections.

## 4. Database Initialization
- [x] **Create Schema Script**:
    - Write a SQL script or C# method to create the `Users`, `Roles`, `UserRoles`, `UserClaims`, `UserLogins`, `UserTokens` and `LocalStore` tables on startup.
- [x] **Implement Database Initialization State**:
    - `IDatabaseInitializationState` interface in Application layer.
    - `DatabaseInitializationState` implementation in Infrastructure.Sqlite.
    - `SqliteDatabaseBootstrapperWorker` signals completion after all bootstrappers run.
    - Infrastructure services wait for initialization before accessing database.

## 5. Dependency Injection & Wiring
- [x] **Update `Application.cs`**:
    - Remove `Application.Identity.Interfaces.IUserStore` (use `Microsoft.AspNetCore.Identity.IUserStore`).
    - Remove `InMemoryUserStore`.
    - Register `SqliteConnectionFactory`.
    - Register Identity with custom stores:
      ```csharp
      services.AddIdentityCore<User>()
          .AddRoles<Role>()
          .AddUserStore<CustomUserStore>()
          .AddRoleStore<CustomRoleStore>()
          .AddSignInManager(); // Needed for Blazor
      ```
- [x] **Update `Presentation.WebApp`**:
    - Ensure Blazor Auth components use the standard `UserManager` and `SignInManager`.

## 6. Blazor Identity UI Implementation (Replicating Template Features)
- [x] **Implement Identity Components (`Presentation.WebApp`)**:
    - **Account Pages**: `Login`, `Register`, `ResendEmailConfirmation`, `ForgotPassword`, `ResetPassword`, `ConfirmEmail`.
    - **Manage Pages**: `Index` (Profile), `Email`, `ChangePassword`, `TwoFactorAuthentication`, `PersonalData`.
    - **Shared Components**: `ExternalLoginPicker`, `ManageNavMenu`, `StatusMessage`.
- [x] **Implement Identity Services**:
    - `IdentityUserAccessor`: Helper to retrieve current user in components.
    - `IdentityRedirectManager`: Helper for handling redirects (login/logout).
    - `IEmailSender`: Implement `IdentityNoOpEmailSender` or real sender.
- [x] **Configure Auth State**:
    - Implement `PersistingRevalidatingAuthenticationStateProvider`.
    - Configure `CascadingAuthenticationState`.
- [x] **Map Endpoints**:
    - Ensure `MapAdditionalIdentityEndpoints` is implemented for Cookie/Token management.

## 7. Cleanup
- [x] **Clean `src/Application`**:
    - Remove `src/Application/LocalStore/Services/SqliteLocalStoreService.cs` (moved to Infra).
    - Remove `src/Application/Identity/Services/InMemoryUserStore.cs` (replaced by CustomUserStore).
- [x] **Clean `src/Presentation.WebApp`**:
    - Remove `src/Presentation.WebApp/Data/ApplicationDbContext.cs` (if exists).
    - Remove `src/Presentation.WebApp/Data/ApplicationUser.cs` (if exists).

## 8. Testing (Playwright)
- [x] **Create `tests/Presentation.WebApp.Tests`**:
    - Add `Microsoft.Playwright.NUnit`.
- [x] **Setup Test Fixture**:
    - Configure `WebApplicationFactory` to run the app in-memory or on a test port.
- [x] **Implement Comprehensive Auth Test Suites (Target: 200+ Cases)**: **ACHIEVED: 267 tests**
    - **Registration Suite** (`RegistrationTests.cs`):
        - Valid registration.
        - Duplicate email/username.
        - Weak passwords (length, complexity).
        - Invalid email formats.
    - **Login Suite** (`LoginTests.cs`):
        - Valid credentials.
        - Invalid password.
        - Non-existent user.
        - Locked out account.
        - Email not confirmed.
    - **Session Management Suite** (`SessionTests.cs`):
        - Logout functionality.
        - Session timeout.
        - Concurrent sessions (if applicable).
        - Cookie persistence.
    - **Password Management Suite** (`PasswordResetTests.cs`):
        - Forgot password flow.
        - Reset password with valid/invalid tokens.
        - Change password (authenticated).
    - **Profile Management Suite** (`ProfileManagementTests.cs`):
        - Update profile details.
        - Change email (confirmation flow).
        - Two-Factor Authentication (enable/disable/verify).
        - Download personal data.
        - Delete account.
    - **Authorization Suite** (`AuthorizationTests.cs`):
        - Access protected routes (redirect to login).
        - Access authorized routes (role-based).
        - Access denied routes (insufficient permissions).
        - **Horizontal Privilege Escalation**: User A trying to access User B's data/profile.
        - **Vertical Privilege Escalation**: Regular user trying to access Admin routes.
    - **Edge Cases & Security** (`SecurityTests.cs`, `EdgeCaseTests.cs`):
        - SQL Injection attempts (via inputs).
        - XSS attempts.
        - URL manipulation.
        - CSRF protection checks.
    - **Additional Test Files**:
        - `SmokeTests.cs`: Basic health checks
        - `NavigationTests.cs`: UI navigation flows
        - `FormValidationTests.cs`: Client-side validation
        - `UITests.cs`: UI element tests
        - `E2EFlowTests.cs`: End-to-end user journeys
        - `TwoFactorAuthTests.cs`: 2FA specific tests
        - `EmailTests.cs`: Email confirmation flows
        - `HttpTests.cs`: HTTP-level tests
        - `AccessibilityTests.cs`: Accessibility compliance
        - `PerformanceTests.cs`: Performance benchmarks

## 9. Current Status

### Completed ✅
- All 267 tests passing (Domain: 33, Application: 58, WebApp: 176)
- Raw SQLite infrastructure with custom Identity stores
- Database initialization state tracking (services wait for DB to be ready)
- Proper folder structure for Infrastructure projects:
  - `Infrastructure.Sqlite`: Base SQLite functionality
  - `Infrastructure.Sqlite.Identity`: User/Role stores
  - `Infrastructure.Sqlite.LocalStore`: Key-value storage
- Internal extension methods (not exposed publicly)
- Comprehensive Playwright E2E test coverage
- CI/CD pipeline with cross-platform tests (Windows + Ubuntu)

### Architecture Notes
- Extension methods in Infrastructure projects are `internal` (used only by Infrastructure classes)
- Tests register services directly instead of using internal extensions
- `IDatabaseInitializationState` interface in Application layer
- `DatabaseInitializationState` implementation signals when DB is ready
- `SqliteLocalStoreService.Open()` waits for initialization automatically

### Test Summary
| Project | Tests |
|---------|-------|
| Domain.Tests | 33 |
| Application.Tests | 58 |
| Presentation.WebApp.Tests | 176 |
| **Total** | **267** |

---

## 10. EF Core Infrastructure Implementation (Proving Domain/Application Stability)

**Goal**: Implement Entity Framework Core with SQLite as an alternative infrastructure layer, proving that the Domain and Application layers are truly persistence-ignorant. This validates the architecture by demonstrating that infrastructure can be swapped without modifying core business logic.

**Success Criteria**: All existing tests pass without any changes to Domain or Application layers.

### 10.1 Project Structure

- [x] **Implement `src/Infrastructure.EFCore`**:
    - Target: `net10.0`
    - Dependencies: `Microsoft.EntityFrameworkCore`
    - Contains:
        - `IEFCoreDatabaseBootstrap`: Interface for database setup
        - `EFCoreDatabaseInitializationState`: Implementation of `IDatabaseInitializationState`
        - `EFCoreDatabaseBootstrapperWorker`: Hosted service for DB migrations
    - **Folder Structure**:
        - Root: `InfrastructureEFCore.cs` (ApplicationDependency)
        - `Extensions/`: `EFCoreInfrastructureServiceCollectionExtensions`
        - `Interfaces/`: `IEFCoreDatabaseBootstrap`
        - `Services/`: `EFCoreDatabaseInitializationState`
        - `Workers/`: `EFCoreDatabaseBootstrapperWorker`

- [x] **Implement `src/Infrastructure.EFCore.Sqlite`**:
    - Target: `net10.0`
    - Dependencies: `Microsoft.EntityFrameworkCore.Sqlite`, `Infrastructure.EFCore`
    - Contains:
        - `SqliteDbContext`: SQLite-specific DbContext with entity configurations
        - `SqliteDatabaseBootstrap`: Runs EnsureCreatedAsync on startup
        - SQLite connection string configuration
    - **Folder Structure**:
        - Root: `InfrastructureEFCoreSqlite.cs` (ApplicationDependency)
        - `SqliteDbContext.cs`: DbContext with User, Role, LocalStoreEntry configurations
        - `Extensions/`: `EFCoreSqliteServiceCollectionExtensions`
        - `Services/`: `SqliteDatabaseBootstrap`

- [x] **Create `src/Infrastructure.EFCore.Identity`** (New):
    - Target: `net10.0`
    - Dependencies: `Microsoft.AspNetCore.Identity`, `Microsoft.Extensions.Identity.Core`, `Infrastructure.EFCore.Sqlite`, `Application`
    - Contains:
        - `EFCoreUserStore`: EF Core implementation of Identity user store interfaces
        - `EFCoreRoleStore`: EF Core implementation of Identity role store interfaces
        - `EFCoreRoleRepository`: Implementation of `IRoleRepository` and `IRoleLookup`
    - **Folder Structure**:
        - Root: `EFCoreIdentityInfrastructure.cs` (ApplicationDependency)
        - `Extensions/`: `EFCoreIdentityServiceCollectionExtensions`
        - `Services/`: `EFCoreUserStore`, `EFCoreRoleStore`, `EFCoreRoleRepository`

- [x] **Create `src/Infrastructure.EFCore.LocalStore`** (New):
    - Target: `net10.0`
    - Dependencies: `Infrastructure.EFCore.Sqlite`, `Application`
    - Contains:
        - `EFCoreLocalStoreService`: EF Core implementation of `ILocalStoreService`
    - **Folder Structure**:
        - Root: `EFCoreLocalStoreInfrastructure.cs` (ApplicationDependency)
        - `Extensions/`: `EFCoreLocalStoreServiceCollectionExtensions`
        - `Services/`: `EFCoreLocalStoreService`

### 10.2 Entity Configurations (EF Core)

- [x] **User Entity Configuration**:
    - Map `User` domain entity to `Users` table
    - Configure Id as Guid with string conversion for SQLite
    - Map all Identity fields (PasswordHash, SecurityStamp, etc.)
    - Ignore navigation properties (PermissionGrants, RoleAssignments, IdentityLinks)

- [x] **Role Entity Configuration**:
    - Map `Role` domain entity to `Roles` table
    - Configure Id as Guid with string conversion for SQLite
    - Map Code, Name, Description, IsSystemRole
    - Ignore navigation property (PermissionGrants)

- [x] **LocalStoreEntry Entity Configuration**:
    - Simple key-value with Group support
    - Primary key on (Group, Id)

### 10.3 Implementation Details

- [x] **Value Converters for IDs**:
    - Guid ↔ String converter for SQLite compatibility
    - Register in DbContext `OnModelCreating`

- [x] **EFCoreUserStore Implementation**:
    - Implement same interfaces as `CustomUserStore`:
        - `IUserStore<User>`
        - `IUserPasswordStore<User>`
        - `IUserEmailStore<User>`
        - `IUserRoleStore<User>`
        - `IUserSecurityStampStore<User>`
        - `IUserLockoutStore<User>`
        - `IUserPhoneNumberStore<User>`
        - `IUserTwoFactorStore<User>`
        - `IUserLoginStore<User>`
        - `IUserAuthenticatorKeyStore<User>`
        - `IUserTwoFactorRecoveryCodeStore<User>`
    - Uses domain entity methods directly (no Hydrate needed for reads as EF tracks entities)

- [x] **EFCoreRoleStore Implementation**:
    - Implement `IRoleStore<Role>`
    - Uses domain entity methods directly

- [x] **EFCoreRoleRepository Implementation**:
    - Implement `IRoleRepository` and `IRoleLookup` interfaces from Application layer

- [x] **EFCoreLocalStoreService Implementation**:
    - Implement `ILocalStoreService` interface
    - Support Open/Close pattern with database initialization wait
    - Uses DbContextFactory for proper scoping

### 10.4 Testing Strategy

- [x] **Verify Core Tests Pass**:
    - Run Domain.Tests (31 tests) - passes unchanged ✅
    - Run Application.Tests (58 tests) - passes unchanged ✅

- [ ] **Integration Testing with EF Core**:
    - Create test fixture that uses EF Core infrastructure
    - Run Presentation.WebApp.Tests with EF Core infrastructure

- [ ] **Add Infrastructure-Specific Tests** (Optional):
    - EF Core migration tests
    - Concurrent access tests
    - Transaction rollback tests

### 10.5 Integration

- [ ] **Update Presentation.WebApp**:
    - Add configuration option to select infrastructure (Raw SQLite vs EF Core)
    - Default to Raw SQLite for backward compatibility
    - Example: `"Infrastructure": "EFCore"` or `"Infrastructure": "RawSqlite"`

- [ ] **Update CI/CD Pipeline**:
    - Add test matrix for both infrastructure implementations
    - Ensure all 267+ tests pass with both backends

### 10.6 Documentation

- [ ] **Update README.md**:
    - Document infrastructure options
    - Explain how to switch between implementations

- [ ] **Architecture Decision Record**:
    - Document why both implementations exist
    - Performance comparison notes
    - Use case recommendations

---

### Current EF Core Implementation Status

**Completed ✅**:
- `Infrastructure.EFCore` base project with initialization state and bootstrap worker
- `Infrastructure.EFCore.Sqlite` with DbContext and entity configurations
- `Infrastructure.EFCore.Identity` with User/Role stores and repositories
- `Infrastructure.EFCore.LocalStore` with local storage service
- All projects added to solution and compile successfully
- Domain.Tests (31) and Application.Tests (58) pass - proving Domain/Application layer stability

**Projects Created**:
| Project | Status | Description |
|---------|--------|-------------|
| Infrastructure.EFCore | ✅ Complete | Base EF Core with initialization |
| Infrastructure.EFCore.Sqlite | ✅ Complete | SQLite provider with DbContext |
| Infrastructure.EFCore.Identity | ✅ Complete | User/Role stores |
| Infrastructure.EFCore.LocalStore | ✅ Complete | Local storage service |

**Next Steps**:
1. Wire up EF Core infrastructure to Presentation.WebApp for integration testing
2. Run full test suite with EF Core backend
3. Add configuration switch between Raw SQLite and EF Core

---

## Summary: Proving Persistence Ignorance

The successful implementation of EF Core infrastructure without modifying Domain or Application layers will prove:

1. **Domain Layer Stability**: `User`, `Role`, and other entities work with any ORM or raw SQL
2. **Application Layer Stability**: Interfaces like `IUserStore`, `IRoleRepository`, `ILocalStoreService` are truly abstract
3. **Clean Architecture**: Infrastructure is a pluggable detail, not a core concern
4. **Native AOT Compatibility**: Both implementations use `Hydrate()` factories, no reflection

