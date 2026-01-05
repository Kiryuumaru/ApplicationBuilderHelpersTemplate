# Authentication API

**Status:** ✅ Complete

## Overview

JWT-based authentication for the REST API. Supports login, registration, token refresh, password management, two-factor authentication (2FA), passkeys (WebAuthn), session management, and anonymous (guest) authentication.

## Anonymous Authentication (Guest Mode)

Zero-friction onboarding allowing users to start playing immediately without signup. Users can later upgrade their anonymous account by linking credentials (password, OAuth, passkey).

### User Flow

```
User visits → POST /auth/register (empty body) → Anonymous account created
                                                        ↓
                                    User interacts with the application
                                                        ↓
                               Prompt: "Save progress?" → POST /auth/link/password
                                                        ↓
                                        Same userId, all data preserved
```

### Anonymous User Creation

The `POST /api/v1/auth/register` endpoint supports anonymous registration with an empty body:

**Anonymous Registration:**
```json
POST /api/v1/auth/register
{}
```

**Response (201 Created):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1...",
  "refreshToken": "eyJhbGciOiJIUzI1...",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "username": null,
    "email": null,
    "isAnonymous": true,
    "roles": ["user"],
    "permissions": ["users._read", "users._write", ...]
  }
}
```

### Registration Behavior Matrix

| Provided | Result |
|----------|--------|
| Nothing | Anonymous account (`isAnonymous: true`, `username: null`) |
| Email only | Anonymous with email linked |
| Username + Password | Full account (`isAnonymous: false`) |
| All fields | Full account with email |

### Domain Model

Anonymous users are represented using the standard `User` entity:

| Property | Anonymous User | Regular User |
|----------|---------------|--------------|
| `Id` | Generated UUID | Generated UUID |
| `UserName` | `null` | Required (unique) |
| `NormalizedUserName` | `null` | Required (unique, uppercase) |
| `Email` | Optional | Optional |
| `PasswordHash` | `null` | Required |
| `IsAnonymous` | `true` | `false` |
| `LinkedAt` | `null` | Set when upgraded from anonymous |

### Key Design Decisions

1. **Nullable UserName:** Anonymous users have no username. The `UserName` and `NormalizedUserName` properties are nullable (`string?`) to properly represent this state.

2. **IsAnonymous Flag:** Boolean flag indicating account type. Once `false`, cannot be reverted.

3. **LinkedAt Timestamp:** Records when an anonymous account was upgraded to a full account.

4. **No Merging:** Anonymous accounts cannot be merged with existing accounts. If a user tries to link credentials already associated with another account, the operation is rejected with 409 Conflict.

### Background Cleanup

The `AnonymousUserCleanupWorker` background service automatically deletes abandoned anonymous accounts:
- **Criteria:** 30+ days inactive AND no trades
- **Frequency:** Runs daily

## URL Structure for Admin Access

Most authenticated endpoints that operate on user-specific data include `{userId}` in the URL path rather than implicitly using the JWT's user ID. This design enables:

- **Regular users**: Can only access their own data (permission system validates `userId` matches JWT)
- **Admins**: Can access any user's data by specifying their `userId` in the URL

**Example:**
- `GET /api/v1/auth/users/{userId}/sessions` - Admin can view any user's sessions
- Regular users are restricted to their own `userId` by the permission system

**Exceptions:**
- `GET /api/v1/auth/me` - Always returns current user's info (from JWT)
- `POST /api/v1/auth/logout` - Always logs out current session (from JWT)

## Endpoints

### Authentication (anonymous)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/auth/login` | Authenticate with username/password |
| POST | `/api/v1/auth/login/2fa` | Complete login with 2FA code or recovery code |
| POST | `/api/v1/auth/login/passkey` | Authenticate with passkey (WebAuthn) |
| POST | `/api/v1/auth/login/passkey/options` | Get passkey login challenge |
| POST | `/api/v1/auth/register` | Create new user account |
| POST | `/api/v1/auth/refresh` | Refresh expired access token |
| POST | `/api/v1/auth/forgot-password` | Request password reset email |
| POST | `/api/v1/auth/reset-password` | Reset password with token |
| GET | `/api/v1/auth/external/providers` | List available OAuth providers |
| POST | `/api/v1/auth/external/{provider}` | Initiate OAuth login flow |
| POST | `/api/v1/auth/external/callback` | Process OAuth callback |

### Session Management (authenticated)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/auth/logout` | Invalidate current session |
| GET | `/api/v1/auth/users/{userId}/sessions` | List all active sessions |
| DELETE | `/api/v1/auth/users/{userId}/sessions/{id}` | Revoke specific session |
| DELETE | `/api/v1/auth/users/{userId}/sessions` | Revoke all sessions (logout everywhere) |

### Profile (authenticated)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/auth/me` | Get current user info |

### Two-Factor Authentication (authenticated)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/auth/users/{userId}/2fa/setup` | Get 2FA setup info (shared key, QR code URI) |
| POST | `/api/v1/auth/users/{userId}/2fa/enable` | Enable 2FA with verification code |
| POST | `/api/v1/auth/users/{userId}/2fa/disable` | Disable 2FA (requires password) |
| POST | `/api/v1/auth/users/{userId}/2fa/recovery-codes` | Generate new recovery codes |

### Identity Management (authenticated)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/auth/users/{userId}/identity` | Get all identity info (username, email, methods) |
| PUT | `/api/v1/auth/users/{userId}/identity/username` | Change username |
| POST | `/api/v1/auth/users/{userId}/identity/password` | Link password (OAuth-only users) |
| PUT | `/api/v1/auth/users/{userId}/identity/password` | Change password |
| POST | `/api/v1/auth/users/{userId}/identity/email` | Link email address |
| PUT | `/api/v1/auth/users/{userId}/identity/email` | Change email address |
| DELETE | `/api/v1/auth/users/{userId}/identity/email` | Unlink email address |
| GET | `/api/v1/auth/users/{userId}/identity/passkeys` | List registered passkeys |
| POST | `/api/v1/auth/users/{userId}/identity/passkeys` | Register new passkey |
| POST | `/api/v1/auth/users/{userId}/identity/passkeys/options` | Get passkey registration challenge |
| POST | `/api/v1/auth/users/{userId}/identity/passkeys/link` | Link passkey (upgrades anonymous users) |
| PUT | `/api/v1/auth/users/{userId}/identity/passkeys/{id}` | Rename passkey |
| DELETE | `/api/v1/auth/users/{userId}/identity/passkeys/{id}` | Delete passkey |
| DELETE | `/api/v1/auth/users/{userId}/identity/external/{provider}` | Unlink OAuth provider |

## Token Structure

Authentication uses a permission-based token separation model where access tokens and refresh tokens have distinct permission scopes, enforced by the permission system (not just token type claims).

### Token Generation Architecture

Token generation is handled by `IUserTokenService` in the Application layer, ensuring clean separation from presentation concerns:

```csharp
public interface IUserTokenService
{
    Task<UserTokenResult> CreateSessionWithTokensAsync(
        string userId, string? username, Guid? correlationId,
        string deviceType, string deviceId, string deviceModel);
    
    Task<UserTokenResult> RotateTokensAsync(Guid sessionId, Guid? correlationId);
}
```

The `UserTokenResult` includes:
- `AccessToken` - JWT with user permissions
- `RefreshToken` - JWT with limited refresh scope
- `SessionId` - Unique session identifier
- `ExpiresInSeconds` - Token expiration time

### Access Token
- **Expiration:** 60 minutes
- **Contains:** User ID, username, roles, permissions (scope)
- **Scope:** User's permissions + `deny;api:auth:refresh`
- **Use:** `Authorization: Bearer {accessToken}`
- **Can:** Access all API endpoints the user has permissions for
- **Cannot:** Be used to refresh tokens (blocked by deny directive)

### Refresh Token
- **Expiration:** 7 days
- **Contains:** User ID, username, session ID
- **Scope:** `allow;api:auth:refresh;userId={userId}` (only)
- **Use:** Send in request body to `/auth/refresh` endpoint
- **Can:** Refresh tokens, access `[Authorize]`-only endpoints (e.g., `/me`, `/logout`)
- **Cannot:** Access endpoints with `[RequiredPermission]` (e.g., `/users`, `/accounts`)

### Token Separation Security Model

```
┌─────────────────────────────────────────────────────────────┐
│                    ACCESS TOKEN                             │
│  Scope: allow;api:accounts:_read, allow;api:users:_read,   │
│         ... user permissions ..., deny;api:auth:refresh    │
├─────────────────────────────────────────────────────────────┤
│  ✅ Can access /api/v1/accounts                            │
│  ✅ Can access /api/v1/auth/me                             │
│  ✅ Can access /api/v1/auth/logout                         │
│  ❌ Cannot refresh tokens (denied by scope)                │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                    REFRESH TOKEN                            │
│  Scope: allow;api:auth:refresh;userId={userId}             │
├─────────────────────────────────────────────────────────────┤
│  ✅ Can refresh tokens at /api/v1/auth/refresh             │
│  ✅ Can access /api/v1/auth/me (Authorize-only)            │
│  ✅ Can access /api/v1/auth/logout (Authorize-only)        │
│  ❌ Cannot access /api/v1/accounts (lacks permission)      │
│  ❌ Cannot access /api/v1/users (lacks permission)         │
└─────────────────────────────────────────────────────────────┘
```

**Key Points:**
- The `/auth/refresh` endpoint is `[AllowAnonymous]` and validates the token in the request body
- Access tokens cannot be used as refresh tokens because they have `deny;api:auth:refresh`
- Refresh tokens can access `[Authorize]`-only endpoints but not `[RequiredPermission]` endpoints
- This model prevents stolen access tokens from being used to generate new tokens

## Request/Response Examples

### Login

**Request:**
```json
POST /api/v1/auth/login
{
  "username": "player1",
  "password": "SecurePassword123!"
}
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1...",
  "refreshToken": "eyJhbGciOiJIUzI1...",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "username": "player1",
    "email": "player1@example.com",
    "roles": ["user"],
    "permissions": ["users._read", "users._write", ...]
  }
}
```

### Register

**Request:**
```json
POST /api/v1/auth/register
{
  "username": "newplayer",
  "password": "SecurePassword123!",
  "email": "newplayer@example.com"
}
```

**Response (201 Created):** Same as login response

### Refresh Token

**Request:**
```json
POST /api/v1/auth/refresh
{
  "refreshToken": "eyJhbGciOiJIUzI1..."
}
```

**Response (200 OK):** New access and refresh tokens

### Change Password

**Request:**
```json
PUT /api/v1/auth/users/{userId}/identity/password
Authorization: Bearer eyJhbGciOiJIUzI1...
{
  "currentPassword": "OldPassword123!",
  "newPassword": "NewSecurePassword456!"
}
```

**Response (204 No Content):** Password changed successfully

**Error Responses:**
- 401: Current password incorrect
- 400: New password doesn't meet requirements

### Get Current User

**Request:**
```
GET /api/v1/auth/me
Authorization: Bearer eyJhbGciOiJIUzI1...
```

**Response (200 OK):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "username": "player1",
  "email": "player1@example.com",
  "roles": ["user"],
  "permissions": ["users._read", "users._write", ...]
}
```

### Forgot Password

Request a password reset email. Always returns 204 to prevent email enumeration attacks.

**Request:**
```json
POST /api/v1/auth/forgot-password
{
  "email": "player1@example.com"
}
```

**Response (204 No Content):** Request received (email sent if account exists)

**Note:** For security reasons, the endpoint always returns 204 regardless of whether the email exists. The reset link is sent via email with a URL-encoded token.

### Reset Password

Reset password using a token from the password reset email.

**Request:**
```json
POST /api/v1/auth/reset-password
{
  "email": "player1@example.com",
  "token": "CfDJ8NZGg8X...",
  "newPassword": "NewSecurePassword456!"
}
```

**Response (204 No Content):** Password reset successfully

**Error Responses:**
- 400: Invalid or expired token, user not found, or password doesn't meet requirements

## Two-Factor Authentication (2FA)

### 2FA Setup

Get the shared key and authenticator URI for setting up 2FA.

**Request:**
```
GET /api/v1/auth/users/{userId}/2fa/setup
Authorization: Bearer eyJhbGciOiJIUzI1...
```

**Response (200 OK):**
```json
{
  "sharedKey": "JBSWY3DPEHPK3PXP",
  "formattedSharedKey": "jbsw y3dp ehpk 3pxp",
  "authenticatorUri": "otpauth://totp/AppName:user1?secret=JBSWY3DPEHPK3PXP&issuer=AppName&digits=6"
}
```

### Enable 2FA

Enable 2FA by verifying a TOTP code from the authenticator app.

**Request:**
```json
POST /api/v1/auth/users/{userId}/2fa/enable
Authorization: Bearer eyJhbGciOiJIUzI1...
{
  "verificationCode": "123456"
}
```

**Response (200 OK):**
```json
{
  "recoveryCodes": [
    "AAAA-BBBB",
    "CCCC-DDDD",
    "EEEE-FFFF",
    ...
  ]
}
```

**Error Responses:**
- 400: Invalid verification code
- 401: Not authenticated

### Disable 2FA

Disable 2FA (requires password confirmation).

**Request:**
```json
POST /api/v1/auth/users/{userId}/2fa/disable
Authorization: Bearer eyJhbGciOiJIUzI1...
{
  "password": "CurrentPassword123!"
}
```

**Response (204 No Content):** 2FA disabled successfully

**Error Responses:**
- 400: Invalid password
- 401: Not authenticated

### Login with 2FA

Complete login when 2FA is required. The regular login endpoint returns 202 Accepted when 2FA is required.

**Regular Login Response (202 Accepted) when 2FA required:**
```json
{
  "requiresTwoFactor": true,
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Two-factor authentication required. Please provide the verification code."
}
```

**2FA Login Request:**
```json
POST /api/v1/auth/login/2fa
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "code": "123456"
}
```

**Response (200 OK):** Same as regular login response (JWT tokens + user info)

**Note:** The code can be either a 6-digit TOTP code or a recovery code. Recovery codes can only be used once.

### Generate Recovery Codes

Generate a new set of 10 recovery codes. This invalidates any previous recovery codes.

**Request:**
```
POST /api/v1/auth/users/{userId}/2fa/recovery-codes
Authorization: Bearer eyJhbGciOiJIUzI1...
```

**Response (200 OK):**
```json
{
  "recoveryCodes": [
    "AAAA-BBBB",
    "CCCC-DDDD",
    "EEEE-FFFF",
    "GGGG-HHHH",
    "IIII-JJJJ",
    "KKKK-LLLL",
    "MMMM-NNNN",
    "OOOO-PPPP",
    "QQQQ-RRRR",
    "SSSS-TTTT"
  ],
  "message": "Store these recovery codes in a safe place. Each code can only be used once."
}
```

**Error Responses:**
- 400: Two-factor authentication must be enabled first
- 401: Not authenticated

## Error Responses

| Status | Scenario |
|--------|----------|
| 401 | Invalid credentials, expired token |
| 403 | Account locked |
| 409 | Username already exists (register) |
| 400 | Invalid password format (register) |

## Test Coverage

Tests are organized into modular test files for maintainability:

### Login Tests (LoginApiTests.cs - 16 tests)

| Test | Description |
|------|-------------|
| Login_WithValidCredentials_ReturnsJwtToken | Valid login returns JWT tokens |
| Login_WithInvalidPassword_Returns401 | Wrong password returns 401 |
| Login_WithNonExistentUser_Returns401 | Unknown user returns 401 |
| Login_WithEmptyBody_Returns400 | Empty request body returns 400 |
| Login_WithMalformedJson_Returns400 | Invalid JSON returns 400 |
| Login_WithSpoofedHeaders_DoesNotBypassAuth | Spoofed headers don't bypass auth |
| Login_WithFakeAuthHeader_DoesNotBypassLogin | Fake auth header doesn't bypass |
| Login_TimingForValidVsInvalidUser_ShouldBeSimilar | Timing attack resistance |
| Login_InvalidCredentials_DoesNotLeakUserExistence | No user enumeration |
| Login_ErrorResponse_DoesNotContainStackTrace | No stack traces in errors |
| Login_WithExtremelyLongUsername_Returns400OrDoesNotCrash | Long username handling |
| Login_WithExtremelyLongPassword_Returns400OrDoesNotCrash | Long password handling |
| Login_UsernameIsCaseInsensitive | Username case insensitivity |
| Login_PasswordIsCaseSensitive | Password case sensitivity |
| Login_WithUnicodeUsername_WorksCorrectly | Unicode username support |
| Login_WithSpecialCharactersInPassword_WorksCorrectly | Special characters support |

### Registration Tests (RegisterApiTests.cs - 13 tests)

| Test | Description |
|------|-------------|
| Register_WithValidData_CreatesUserAndReturnsTokens | Valid registration returns tokens |
| Register_WithExistingUsername_Returns409Conflict | Duplicate username returns 409 |
| Register_WithMismatchedPasswords_Returns400 | Mismatched passwords returns 400 |
| Register_WithDuplicateEmail_Returns409OrSuccess | Duplicate email handling |
| Register_WithEmptyBody_Returns400 | Empty body returns 400 |
| Register_WithMalformedJson_Returns400 | Malformed JSON returns 400 |
| Register_ConflictResponse_DoesNotLeakUserDetails | No user detail leakage |
| Register_WithExtremelyLongUsername_Returns400 | Long username validation |
| Register_WithExtremelyLongEmail_Returns400OrSucceeds | Long email handling |
| Register_ErrorResponse_DoesNotContainStackTrace | No stack traces in errors |
| Register_SuccessResponse_DoesNotContainPassword | No password in response |
| Register_UsernameNormalization_PreventsCaseDuplicates | Case-insensitive usernames |
| Register_EmailNormalization_IsCaseInsensitive | Case-insensitive emails |

### Token Tests (TokenApiTests.cs - 19 tests)

| Test | Description |
|------|-------------|
| RefreshToken_WithValidToken_ReturnsNewTokens | Valid refresh works |
| RefreshToken_WithInvalidToken_Returns401 | Invalid refresh returns 401 |
| RefreshToken_WithAccessTokenInsteadOfRefresh_Returns401 | Access token rejected |
| RefreshToken_WithEmptyToken_Returns400Or401 | Empty token handling |
| RefreshToken_WithMalformedJson_Returns400 | Malformed JSON handling |
| RefreshToken_RotatesToken_OldTokenBecomesInvalid | Token rotation |
| RefreshToken_NewTokenWorks_AfterRotation | New token works |
| Logout_WithValidToken_ReturnsNoContent | Logout returns 204 |
| Logout_WithoutToken_Returns401 | Logout requires auth |
| Logout_InvalidatesRefreshToken | Logout invalidates refresh |
| GetMe_WithValidToken_ReturnsUserInfo | GetMe returns user info |
| GetMe_WithNoToken_Returns401 | GetMe requires token |
| GetMe_WithInvalidToken_Returns401 | Invalid token rejected |
| GetMe_WithModifiedTokenPayload_Returns401 | Modified payload rejected |
| GetMe_WithStrippedSignature_Returns401 | Unsigned token rejected |
| GetMe_WithNoneAlgorithmAttack_Returns401 | None algorithm rejected |
| AccessToken_StillWorks_AfterRefresh | Old access token works |
| RefreshToken_CannotBeUsedByDifferentUser | Cross-user prevention |
| RefreshToken_Response_DoesNotLeakSensitiveHeaders | No sensitive headers |

### Token Expiration Tests (TokenExpirationTests.cs - 25 tests)

| Test | Description |
|------|-------------|
| Login_Response_ExpiresIn_IsCorrectValue | Correct expiration value |
| Register_Response_ExpiresIn_IsCorrectValue | Correct expiration value |
| Refresh_Response_ExpiresIn_IsCorrectValue | Correct expiration value |
| ExpiresIn_IsPositiveNumber | Positive expiration |
| AccessToken_HasExpClaim | Token has exp claim |
| AccessToken_ExpClaimMatchesExpiresIn | Exp matches expiresIn |
| AccessToken_HasIatClaim | Token has iat claim |
| RefreshToken_HasExpClaim | Refresh has exp claim |
| RefreshToken_ExpiresLaterThanAccessToken | Refresh expires later |
| RefreshToken_AfterSessionRevoked_Returns401 | Revoked session fails |
| RefreshToken_AfterAllSessionsRevoked_Returns401 | Revoked all fails |
| RefreshToken_ReusedAfterRotation_DetectsTheft | Theft detection |
| RefreshToken_ReusedMultipleTimes_AllFail | Multiple reuse fails |
| RefreshToken_TheftRevokesEntireSession | Theft revokes session |
| RefreshToken_ChainedRefreshes_EachTokenRotates | Chain rotation works |
| RefreshToken_OldTokenInChain_Invalid | Old chain tokens fail |
| RefreshToken_WithAccessToken_Returns401 | Access token rejected |
| AccessToken_HasCorrectPermissionScope | Correct permission scope |
| RefreshToken_HasCorrectPermissionScope | Correct permission scope |
| RefreshToken_WithEmptyObject_Returns400 | Empty object returns 400 |
| RefreshToken_WithExtraFields_StillWorks | Extra fields allowed |
| RefreshToken_FromDifferentSession_DoesNotAffectOther | Session isolation |
| RefreshToken_FromOtherUser_NotAccepted | Cross-user blocked |
| NewTokens_HaveDifferentValues_EachTime | Unique token values |
| TokenType_IsBearerForAllResponses | Bearer token type |

### Password Tests (PasswordApiTests.cs - 20 tests)

| Test | Description |
|------|-------------|
| ChangePassword_WithValidCredentials_ReturnsNoContent | Valid change returns 204 |
| ChangePassword_OldPasswordNoLongerWorks | Old password fails |
| ChangePassword_NewPasswordWorks | New password works |
| ChangePassword_WithWrongCurrentPassword_Returns401 | Wrong password returns 401 |
| ChangePassword_WithWeakNewPassword_Returns400 | Weak password returns 400 |
| ChangePassword_WithoutAuthentication_Returns401 | Requires auth |
| ChangePassword_WithSamePassword_MayReturn400 | Same password handling |
| ForgotPassword_WithValidEmail_ReturnsNoContent | Valid email returns 204 |
| ForgotPassword_WithNonExistentEmail_ReturnsNoContent | Unknown email returns 204 |
| ForgotPassword_WithInvalidEmailFormat_Returns400 | Invalid format returns 400 |
| ForgotPassword_WithEmptyEmail_Returns400 | Empty email returns 400 |
| ResetPassword_WithInvalidToken_Returns400 | Invalid token returns 400 |
| ResetPassword_WithNonExistentEmail_Returns400 | Unknown email returns 400 |
| ResetPassword_WithWeakPassword_Returns400 | Weak password returns 400 |
| ResetPassword_WithMalformedRequest_Returns400 | Malformed request returns 400 |
| ForgotPassword_ResponseTime_SimilarForExistingAndNonExisting | Timing attack resistance |
| ChangePassword_MultipleWrongAttempts_DoesNotLockAccount | No lockout |
| ChangePassword_ErrorResponse_DoesNotLeakInfo | No info leakage |
| ForgotPassword_Response_DoesNotRevealEmailExistence | No email enumeration |
| ChangePassword_MayInvalidateOtherSessions | Session invalidation |

### Passkey Tests (PasskeyApiTests.cs - 21 tests)

| Test | Description |
|------|-------------|
| PasskeyCreationOptions_WithValidToken_ReturnsOptions | Valid token returns options |
| PasskeyCreationOptions_WithoutToken_Returns401 | Requires auth |
| PasskeyCreationOptions_WithEmptyCredentialName_MayReturn400 | Empty name handling |
| PasskeyLoginOptions_ReturnsOptions | Returns login options |
| PasskeyLoginOptions_WithUsername_ReturnsOptions | Username hint works |
| PasskeyLoginOptions_WithNonExistentUsername_StillReturnsOptions | Unknown user handling |
| PasskeyRegister_WithoutToken_Returns401 | Requires auth |
| PasskeyRegister_WithInvalidChallengeId_Returns400 | Invalid challenge fails |
| PasskeyRegister_WithEmptyChallengeId_Returns400 | Empty challenge fails |
| PasskeyLogin_WithInvalidChallengeId_Returns400 | Invalid challenge fails |
| PasskeyLogin_WithEmptyChallengeId_Returns400 | Empty challenge fails |
| PasskeyLogin_WithMalformedAssertionJson_Returns400Or500 | Malformed JSON handling |
| PasskeyList_WithValidToken_ReturnsEmptyList | List returns empty |
| PasskeyList_WithoutToken_Returns401 | Requires auth |
| PasskeyDelete_WithoutToken_Returns401 | Requires auth |
| PasskeyDelete_WithNonExistentId_Returns404 | Unknown ID returns 404 |
| PasskeyLogin_ChallengeCannotBeReused | Challenge replay prevention |
| PasskeyRegister_ChallengeCannotBeReused | Challenge replay prevention |
| PasskeyDelete_CannotDeleteOtherUserPasskey | Cross-user prevention |
| PasskeyRegister_WithLargeAttestationJson_DoesNotCrash | Large payload handling |
| PasskeyCreationOptions_WithLongCredentialName_DoesNotCrash | Long name handling |

### Two-Factor Tests (TwoFactorApiTests.cs - 19 tests)

| Test | Description |
|------|-------------|
| TwoFactorSetup_WithValidToken_ReturnsSetupInfo | Returns setup info |
| TwoFactorSetup_WithoutToken_Returns401 | Requires auth |
| TwoFactorSetup_SharedKeyIsUnique_PerUser | Unique keys per user |
| TwoFactorEnable_WithInvalidCode_Returns400 | Invalid code returns 400 |
| TwoFactorEnable_WithoutToken_Returns401 | Requires auth |
| TwoFactorDisable_WithoutToken_Returns401 | Requires auth |
| TwoFactorDisable_WhenNotEnabled_Returns400Or500 | Not enabled handling |
| TwoFactorDisable_WithWrongPassword_Returns401 | Wrong password fails |
| TwoFactorLogin_WithInvalidUserId_Returns401 | Invalid user ID fails |
| TwoFactorLogin_WithInvalidCode_Returns401 | Invalid code fails |
| TwoFactorLogin_WithEmptyGuid_Returns401 | Empty GUID fails |
| RecoveryCodes_WithoutTwoFactorEnabled_Returns400 | 2FA required |
| RecoveryCodes_WithoutToken_Returns401 | Requires auth |
| TwoFactorEnable_MultipleWrongCodes_DoesNotLockAccount | No lockout |
| TwoFactorLogin_MultipleWrongCodes_TracksFailures | Failure tracking |
| TwoFactorLogin_TimingForValidVsInvalidUserId_ShouldBeSimilar | Timing attack resistance |
| TwoFactorLogin_SameCodeCannotBeReused | Code replay prevention |
| TwoFactorEnable_ErrorResponse_DoesNotLeakSecretKey | No secret leakage |
| TwoFactorLogin_ErrorResponse_DoesNotRevealIf2FAEnabled | No 2FA status leakage |

### Session Tests (SessionApiTests.cs - 10 tests)

| Test | Description |
|------|-------------|
| ListSessions_AfterRegister_ReturnsOneSession | Single session after register |
| ListSessions_WithoutToken_Returns401 | Requires auth |
| ListSessions_AfterMultipleLogins_ReturnsMultipleSessions | Multiple sessions tracked |
| RevokeSession_CurrentSession_Returns400 | Cannot revoke current |
| RevokeSession_OtherSession_ReturnsNoContent | Revoke other returns 204 |
| RevokeSession_NonExistentId_Returns404 | Unknown ID returns 404 |
| RevokeAllSessions_WithMultipleSessions_RevokesAllExceptCurrent | Revokes all except current |
| RefreshToken_RotatesToken_OldTokenInvalid | Token rotation |
| RefreshToken_WithNewToken_Succeeds | New token works |
| Logout_RevokesCurrentSession | Logout revokes session |

### Session Security Tests (SessionSecurityTests.cs - 17 tests)

| Test | Description |
|------|-------------|
| RevokeSession_WithInvalidGuid_Returns404 | Invalid GUID returns 404 |
| RevokeSession_WithEmptyGuid_Returns404OrBadRequest | Empty GUID handling |
| RevokeSession_OtherUsersSession_Returns404OrForbidden | Cross-user prevention |
| ListSessions_OnlyReturnsOwnSessions | Own sessions only |
| ListSessions_DoesNotLeakOtherUserInfo | No user info leakage |
| ConcurrentSessions_AllWork_Independently | Session independence |
| RevokeOneSession_OthersStillWork | Partial revocation |
| EachLogin_CreatesNewSession | New session per login |
| RevokeAllSessions_CurrentStillWorks | Current session preserved |
| RevokeAllSessions_OthersInvalidated | Others invalidated |
| RevokeAllSessions_ReturnsCorrectCount | Correct revoked count |
| ListSessions_ShowsIsCurrent_Correctly | IsCurrent flag |
| ListSessions_HasCreatedAt | CreatedAt field |
| Logout_RevokesCurrentSessionOnly | Logout precision |
| Logout_MultipleTimesFromSameSession_NoError | Multiple logout safe |
| RevokedSession_AccessToken_Returns401 | Revoked session's access token rejected |
| RevokedSession_RefreshToken_Returns401 | Revoked session's refresh token rejected |

### Security Tests (AuthSecurityTests.cs - 22 tests)

| Test | Description |
|------|-------------|
| JWT_WithNoneAlgorithm_IsRejected | None algorithm rejected |
| JWT_WithModifiedPayload_IsRejected | Modified payload rejected |
| JWT_WithModifiedSignature_IsRejected | Modified signature rejected |
| JWT_WithEmptySignature_IsRejected | Empty signature rejected |
| JWT_FromDifferentKey_IsRejected | Different key rejected |
| Request_WithMultipleAuthHeaders_UsesFirstOrRejects | Multiple auth handling |
| Request_WithSpoofedXForwardedFor_DoesNotBypassSecurity | XFF spoofing blocked |
| Request_WithSpoofedHost_DoesNotCauseIssues | Host spoofing safe |
| Request_WithUrlRewriteHeaders_DoesNotBypassAuth | URL rewrite blocked |
| Login_WithWrongContentType_Returns400Or415 | Content-type validation |
| Login_WithXmlContentType_Returns400Or415 | XML rejected |
| Request_WithCRLFInjection_DoesNotCauseIssues | CRLF injection safe |
| Login_WithUnicodeNormalizationAttack_DoesNotBypass | Unicode attacks blocked |
| Login_WithNullByteInjection_DoesNotBypass | Null byte injection safe |
| Response_HasSecurityHeaders | Security headers present |
| Response_DoesNotCacheAuthTokens | No token caching |
| ErrorResponse_DoesNotLeakStackTrace | No stack traces |
| ErrorResponse_DoesNotLeakDatabaseInfo | No database info |
| ErrorResponse_DoesNotLeakFilePaths | No file paths |
| Login_GeneratesNewSessionId | New session per login |
| ConcurrentLogins_DoNotCauseRaceConditions | Race condition safety |
| ConcurrentRegistrations_PreventDuplicates | Duplicate prevention |

### OAuth Tests (OAuthApiTests.cs - 12 tests)

| Test | Description |
|------|-------------|
| GetProviders_ReturnsAvailableProviders | Returns provider list |
| GetProviders_NoAuthRequired | No auth required |
| InitiateOAuth_WithMockProvider_ReturnsAuthorizationUrl | Returns auth URL |
| InitiateOAuth_WithInvalidProvider_Returns400 | Invalid provider returns 400 |
| InitiateOAuth_WithDisabledProvider_Returns400 | Disabled provider returns 400 |
| OAuthCallback_WithMockProvider_CreatesNewUserAndSession | Creates user and session |
| OAuthCallback_WithEmptyState_Returns400 | Empty state returns 400 |
| GetExternalLogins_WithoutAuth_Returns401 | Requires auth |
| GetExternalLogins_WithAuth_ReturnsEmptyListForNewUser | Empty for new user |
| LinkExternalLogin_WithoutAuth_Returns401 | Requires auth |
| UnlinkExternalLogin_WithoutAuth_Returns401 | Requires auth |
| UnlinkExternalLogin_NonExistentProvider_Returns404 | Unknown provider returns 404 |

### Token Separation Tests (TokenSeparationTests.cs - 10 tests)

| Test | Description |
|------|-------------|
| AccessTokenAsRefreshToken_IsRejected_Returns401 | Access tokens cannot be used in refresh endpoint body |
| RefreshTokenAsRefreshToken_IsAccepted_Returns200 | Refresh tokens can refresh successfully |
| RefreshToken_CannotAccessProtectedEndpoint_Returns403 | Refresh tokens denied by `[RequiredPermission]` |
| AccessToken_CanAccessMe_Returns200 | Access tokens can access `[Authorize]` endpoints |
| AccessToken_CanLogout_ReturnsSuccess | Access tokens can logout |
| RefreshToken_CanAccessMe_BecauseOnlyAuthorizeRequired | Refresh tokens valid for `[Authorize]`-only endpoints |
| RefreshToken_CanLogout_BecauseOnlyAuthorizeRequired | Refresh tokens can logout (only needs valid token) |
| NewAccessToken_AfterRefresh_StillCannotBeUsedAsRefreshToken | New access tokens maintain deny;api:auth:refresh |
| NewRefreshToken_AfterRefresh_CanBeUsedAsRefreshToken | New refresh tokens maintain allow;api:auth:refresh |
| NewRefreshToken_AfterRefresh_StillCannotAccessProtectedEndpoints | New refresh tokens still lack other permissions |

**Total: 308 authentication tests** (includes [Theory] tests with multiple data sets)

## Password Requirements

- Minimum 6 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit
- At least one special character

## Security Features

1. **Session-based tokens:** Each login creates a tracked session with unique ID
2. **Session validation on every request:** Access tokens validate session state on each authenticated request - revoked sessions are immediately rejected
3. **Refresh token rotation:** Each refresh returns a NEW refresh token, old token is invalidated
4. **Token theft detection:** Reusing old refresh token revokes entire session
5. **Timing attack resistance:** Consistent response times for valid/invalid users
6. **No user enumeration:** Same response for existing/non-existing accounts
7. **Security headers:** X-Content-Type-Options, X-Frame-Options, etc.
8. **CRLF/injection protection:** Request sanitization
9. **Challenge replay prevention:** Passkey challenges single-use
