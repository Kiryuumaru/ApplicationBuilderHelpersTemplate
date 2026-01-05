# Application Builder Helpers Template - Documentation

## Feature Documentation

| Feature | Document | Status |
|---------|----------|--------|
| Authentication | [features/authentication.md](features/authentication.md) | ✅ Complete |
| User Management | [features/user-management.md](features/user-management.md) | ✅ Complete |

## Architecture Documentation

| Topic | Document |
|-------|----------|
| Authorization Architecture | [architecture/authorization-architecture.md](architecture/authorization-architecture.md) |
| Test Architecture | [architecture/test-architecture.md](architecture/test-architecture.md) |

## API Documentation

| API | Document |
|-----|----------|
| Authentication | [api/api-auth.md](api/api-auth.md) |
| IAM (Users, Roles, Permissions) | [api/api-iam.md](api/api-iam.md) |

## Quick Start

```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run WebApi
dotnet run --project src/Presentation.WebApi

# Run WebApp
dotnet run --project src/Presentation.WebApp
```

## API Base URL

```
Development: http://localhost:5199
```

## Authentication

All authenticated endpoints require JWT bearer token:

```
Authorization: Bearer {access_token}
```

Get tokens via:
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/register`

## Recent Changes (January 2026)

### Token Generation Refactoring
- Moved token generation logic to Application layer via `IUserTokenService`
- `UserTokenResult` now includes `ExpiresInSeconds` property
- All auth controllers updated to use the new service

### Allow/Deny Permission Grants
- Added `GrantType` enum (`Allow`, `Deny`) for permission grants
- Direct permission grants (via IAM API) are baked into JWT tokens
- Role scope changes take effect immediately (resolved at runtime from DB)
- Added 7 comprehensive Allow/Deny integration tests

### Inline Role Parameters in JWT
- Role claims now use inline parameter format: `USER;roleUserId=abc123`
- Removed separate `role_params:` claims from tokens
- Smaller token size, cleaner format

### `[FromJwt]` Attribute Pattern
- Binds method parameters directly from JWT claims (e.g., `ClaimTypes.NameIdentifier`)
- Used for self-service endpoints like `/auth/me` and `/auth/logout`
- Enables userId-scoped permission grants to apply automatically

See [Authorization Architecture](architecture/authorization-architecture.md) for details.
