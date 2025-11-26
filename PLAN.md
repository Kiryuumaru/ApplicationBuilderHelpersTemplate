# Plan: Raw SQLite & Custom Identity Stores

This plan focuses on maintaining a lightweight, dependency-free architecture by using raw SQLite (ADO.NET) instead of Entity Framework Core. We will implement Microsoft Identity interfaces manually to support standard authentication features without the overhead of an ORM.

## 1. Domain Refactoring (Identity Compatible)
- [ ] **Refactor `User` Entity**:
    - **Strategy**: Keep as pure Domain Entity (No inheritance from `IdentityUser`).
    - **Implementation**: Manually add all standard Identity fields (`PasswordHash`, `SecurityStamp`, `NormalizedEmail`, etc.) to the `User` class.
    - **Reasoning**: This avoids polluting the Domain with `Microsoft.Extensions.Identity.Stores` dependencies while ensuring the `CustomUserStore` can map 1:1 to the Domain entity without complex translation layers.
    - **Actions**:
        - Remove `PasswordCredential` ValueObject.
        - Add: `UserId Id` (Strongly Typed), `UserName`, `NormalizedUserName`, `Email`, `NormalizedEmail`, `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `PhoneNumber`, `TwoFactorEnabled`, `LockoutEnd`, `LockoutEnabled`, `AccessFailedCount`.
    - Keep `PermissionGrants` and `RoleAssignments` (custom logic).
- [ ] **Refactor `Role` Entity**:
    - Match Identity structure (`RoleId Id`, `Name`, `NormalizedName`, `ConcurrencyStamp`).
- [ ] **Maintain Code Generators**:
    - Ensure `Domain.CodeGenerator` (PermissionIds, RoleIds) continues to work.
    - *Note*: `RoleIdsGenerator` reads from `Roles.All`. We must ensure `Role` changes don't break the static definitions.

## 2. Architecture Restructuring (DDD & DI)
- [ ] **Create `src/Infrastructure.Sqlite` Project**:
    - Target `net10.0`.
    - Add references: `Microsoft.Data.Sqlite`.
    - Depends on: `src/Application`.
    - Contains:
        - `SqliteConnectionFactory`: Helper to manage connections.
        - `DatabaseBootstrap`: Base logic/interface for table creation.
        - *Note*: Must handle Type Handlers for Strongly Typed IDs (`UserId`, `RoleId`) in Dapper/Sqlite.

- [ ] **Create `src/Infrastructure.Sqlite.LocalStore` Project**:
    - Target `net10.0`.
    - Depends on: `src/Infrastructure.Sqlite`, `src/Application` (for `ILocalStoreService`).
    - Contains:
        - `SqliteLocalStoreService`: Implementation.
        - `LocalStoreTableInitializer`: Script to create LocalStore table.

- [ ] **Create `src/Infrastructure.Sqlite.Identity` Project**:
    - Target `net10.0`.
    - Add references: `Microsoft.Extensions.Identity.Core`.
    - Depends on: `src/Infrastructure.Sqlite`, `src/Application`, `src/Domain`.
    - Contains:
        - `CustomUserStore`: Implementation of Identity interfaces.
        - `CustomRoleStore`: Implementation of Identity interfaces.
        - `IdentityTableInitializer`: Script to create Users, Roles, UserRoles, UserClaims, etc. tables.
        - *Note*: Stores must convert `UserId` <-> `string/Guid` when talking to Identity interfaces (which often expect strings or Guids).

- [ ] **Create `src/Infrastructure.Sqlite.Authorization` Project** (Optional/Future):
    - Target `net10.0`.
    - Depends on: `src/Infrastructure.Sqlite`, `src/Domain`.
    - Contains: Persistence for custom permission grants if not handled by Identity Claims.

## 3. Infrastructure Implementation (The "Hard" Part)
- [ ] **Implement `CustomUserStore` (in `Infrastructure.Sqlite.Identity`)**:
    - Implement interfaces: `IUserStore<User>`, `IUserPasswordStore<User>`, `IUserEmailStore<User>`, `IUserRoleStore<User>`, `IUserSecurityStampStore<User>`, `IUserLockoutStore<User>`, `IUserPhoneNumberStore<User>`, `IUserTwoFactorStore<User>`, `IUserLoginStore<User>`.
    - Write raw SQL for CRUD operations.
    - **Crucial**: Handle `UserId` conversion in SQL parameters.
- [ ] **Implement `CustomRoleStore` (in `Infrastructure.Sqlite.Identity`)**:
    - Implement `IRoleStore<Role>`.
    - Write raw SQL for CRUD operations.
    - **Crucial**: Handle `RoleId` conversion.
- [ ] **Implement `SqliteLocalStoreService` (in `Infrastructure.Sqlite.LocalStore`)**:
    - Move existing logic from `src/Application/LocalStore` to this new project.
    - Adapt to use `SqliteConnectionFactory`.

## 4. Database Initialization
- [ ] **Create Schema Script**:
    - Write a SQL script or C# method to create the `Users`, `Roles`, `UserRoles`, `UserClaims`, `UserLogins`, `UserTokens` and `LocalStore` tables on startup.

## 5. Dependency Injection & Wiring
- [ ] **Update `Application.cs`**:
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
- [ ] **Update `Presentation.WebApp`**:
    - Ensure Blazor Auth components use the standard `UserManager` and `SignInManager`.

## 6. Blazor Identity UI Implementation (Replicating Template Features)
- [ ] **Implement Identity Components (`Presentation.WebApp`)**:
    - **Account Pages**: `Login`, `Register`, `ResendEmailConfirmation`, `ForgotPassword`, `ResetPassword`, `ConfirmEmail`.
    - **Manage Pages**: `Index` (Profile), `Email`, `ChangePassword`, `TwoFactorAuthentication`, `PersonalData`.
    - **Shared Components**: `ExternalLoginPicker`, `ManageNavMenu`, `StatusMessage`.
- [ ] **Implement Identity Services**:
    - `IdentityUserAccessor`: Helper to retrieve current user in components.
    - `IdentityRedirectManager`: Helper for handling redirects (login/logout).
    - `IEmailSender`: Implement `IdentityNoOpEmailSender` or real sender.
- [ ] **Configure Auth State**:
    - Implement `PersistingRevalidatingAuthenticationStateProvider`.
    - Configure `CascadingAuthenticationState`.
- [ ] **Map Endpoints**:
    - Ensure `MapAdditionalIdentityEndpoints` is implemented for Cookie/Token management.

## 7. Cleanup
- [ ] **Clean `src/Application`**:
    - Remove `src/Application/LocalStore/Services/SqliteLocalStoreService.cs` (moved to Infra).
    - Remove `src/Application/Identity/Services/InMemoryUserStore.cs` (replaced by CustomUserStore).
- [ ] **Clean `src/Presentation.WebApp`**:
    - Remove `src/Presentation.WebApp/Data/ApplicationDbContext.cs` (if exists).
    - Remove `src/Presentation.WebApp/Data/ApplicationUser.cs` (if exists).

## 8. Testing (Playwright)
- [ ] **Create `tests/Presentation.WebApp.Tests`**:
    - Add `Microsoft.Playwright.NUnit`.
- [ ] **Setup Test Fixture**:
    - Configure `WebApplicationFactory` to run the app in-memory or on a test port.
- [ ] **Implement Comprehensive Auth Test Suites (Target: 200+ Cases)**:
    - **Registration Suite**:
        - Valid registration.
        - Duplicate email/username.
        - Weak passwords (length, complexity).
        - Invalid email formats.
    - **Login Suite**:
        - Valid credentials.
        - Invalid password.
        - Non-existent user.
        - Locked out account.
        - Email not confirmed.
    - **Session Management Suite**:
        - Logout functionality.
        - Session timeout.
        - Concurrent sessions (if applicable).
        - Cookie persistence.
    - **Password Management Suite**:
        - Forgot password flow.
        - Reset password with valid/invalid tokens.
        - Change password (authenticated).
    - **Profile Management Suite**:
        - Update profile details.
        - Change email (confirmation flow).
        - Two-Factor Authentication (enable/disable/verify).
        - Download personal data.
        - Delete account.
    - **Authorization Suite**:
        - Access protected routes (redirect to login).
        - Access authorized routes (role-based).
        - Access denied routes (insufficient permissions).
        - **Horizontal Privilege Escalation**: User A trying to access User B's data/profile.
        - **Vertical Privilege Escalation**: Regular user trying to access Admin routes.
    - **Edge Cases & Security**:
        - SQL Injection attempts (via inputs).
        - XSS attempts.
        - URL manipulation.
        - CSRF protection checks.

