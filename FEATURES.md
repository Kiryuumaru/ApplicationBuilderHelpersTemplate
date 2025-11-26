# Features

This template provides a robust, production-ready foundation for .NET applications, featuring a strict Clean Architecture implementation with raw SQLite persistence and a fully custom Microsoft Identity integration.

### Architecture & Technology
- **Clean Architecture (DDD)**: Strict separation between Domain (Business Logic), Application (Use Cases), and Infrastructure (Persistence).
- **Raw SQLite Persistence**: High-performance, dependency-free data access using `Microsoft.Data.Sqlite` and Dapper-style mapping. Eliminates Entity Framework Core overhead for maximum control and speed.
- **Strongly Typed IDs**: Domain entities utilize ValueObjects (`UserId`, `RoleId`) for type safety, mapped seamlessly to database primitives.

### Identity & Security
- **Full Microsoft Identity Compliance**: Includes custom `IUserStore` and `IRoleStore` implementations that support:
    - Password Hashing & Validation.
    - Email Confirmation & Normalization.
    - Account Lockout & Failure Counting.
    - Two-Factor Authentication (2FA).
    - Security Stamps for session invalidation.
- **Pure Domain Entities**: `User` and `Role` entities are pure POCOs with zero dependencies on `Microsoft.AspNetCore.Identity` or EF Core attributes.
- **Robust Authorization**: Integrated Role-based and Permission-based authorization system.

### User Interface (Blazor)
- **Complete Auth UI**: Includes fully functional Blazor components for all standard identity flows:
    - Login / Register / Logout.
    - Forgot / Reset Password.
    - Email Confirmation.
    - Profile Management (Change Email, Password, 2FA).
    - Personal Data Management (Download/Delete).
- **Seamless Integration**: UI components interact with the standard `UserManager` and `SignInManager`, completely decoupled from the custom SQLite backend.

### Quality Assurance
- **End-to-End Testing**: Includes a comprehensive Playwright test suite (200+ cases) ensuring reliability across all authentication flows and security boundaries.
- **Security Hardening**: Built-in protection against common vulnerabilities including SQL Injection, XSS, CSRF, and Privilege Escalation.
