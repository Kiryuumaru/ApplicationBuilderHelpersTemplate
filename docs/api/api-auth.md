# Auth API

> **Controller:** `AuthController`  
> **Base Path:** `/api/v1/auth`  
> **Authentication:** Public (login/register) or Bearer Token

## Overview

Authentication endpoints for user login, registration, token management, and password operations.

---

## Endpoints

### `POST /api/v1/auth/login`

Authenticate user and receive JWT tokens.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "password123"
}
```

**Response:** `200 OK`
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "eyJhbGc...",
  "expiresIn": 3600
}
```

**Errors:**
- `401 Unauthorized` - Invalid credentials

---

### `POST /api/v1/auth/register`

Register a new user account.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "password123",
  "displayName": "John Doe"
}
```

**Response:** `201 Created`

**Errors:**
- `400 Bad Request` - Invalid input
- `409 Conflict` - Email already registered

---

### `POST /api/v1/auth/refresh`

Refresh an expired access token using a valid refresh token.

**Request Body:**
```json
{
  "refreshToken": "eyJhbGc..."
}
```

**Response:** `200 OK`
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "eyJhbGc...",
  "expiresIn": 3600
}
```

**Errors:**
- `401 Unauthorized` - Invalid or expired refresh token
- `401 Unauthorized` - Access token used instead of refresh token

---

### `POST /api/v1/auth/logout`

Invalidate the current session.

**Headers:**
```
Authorization: Bearer {accessToken}
```

**Response:** `200 OK`

---

### `GET /api/v1/auth/me`

Get the current authenticated user's information.

**Headers:**
```
Authorization: Bearer {accessToken}
```

**Response:** `200 OK`
```json
{
  "id": "guid",
  "email": "user@example.com",
  "displayName": "John Doe",
  "roles": ["User"]
}
```

**Errors:**
- `401 Unauthorized` - Not authenticated

---

### `POST /api/v1/auth/forgot-password`

Request a password reset email.

> **Note:** Currently a stub implementation.

**Request Body:**
```json
{
  "email": "user@example.com"
}
```

**Response:** `200 OK`

---

### `POST /api/v1/auth/reset-password`

Reset password using a reset token.

> **Note:** Currently a stub implementation.

**Request Body:**
```json
{
  "token": "reset-token",
  "newPassword": "newpassword123"
}
```

**Response:** `200 OK`

**Errors:**
- `400 Bad Request` - Invalid or expired token

---

## Token Types

### Access Token
- Short-lived (default: 1 hour)
- Used for API authorization
- Contains user claims (id, email, roles)

### Refresh Token
- Long-lived (default: 7 days)
- Used only to obtain new access tokens
- Scope: Only `allow;api:auth:refresh;userId={userId}` permission
- Cannot access `[RequiredPermission]` endpoints (lacks permissions)
- Can access `[Authorize]`-only endpoints like `/me`

---

## Test Coverage

See [features/authentication.md](../features/authentication.md#test-coverage) for comprehensive test coverage details.

**Summary:** 308 authentication tests covering login, registration, tokens, passwords, passkeys, 2FA, sessions, OAuth, and security.
