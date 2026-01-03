# Application Builder Helpers Template - Documentation

## Feature Documentation

| Feature | Document | Status |
|---------|----------|--------|
| Authentication | [features/authentication.md](features/authentication.md) | ✅ Complete |
| Anonymous Auth | [features/anonymous-authentication.md](features/anonymous-authentication.md) | ✅ Complete |
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

## Recent Changes (December 26, 2025)

### `[FromJwt]` Attribute Pattern

Added support for binding method parameters directly from JWT claims:

- **`FromJwtAttribute`** - Specifies which JWT claim to extract (e.g., `ClaimTypes.NameIdentifier`)
- **`FromJwtModelBinder`** - Extracts and converts claim values to parameter types

This pattern is used for self-service endpoints like `/auth/me` and `/auth/logout` to automatically bind the userId from the JWT, enabling userId-scoped permission grants to apply.

See [Authorization Architecture](architecture/authorization-architecture.md#the-fromjwt-pattern) for details.
