using System.Text.Json.Serialization;
using Application.Client.Authentication.Models;
using Application.Client.Iam.Models;

namespace Application.Client.Json;

/// <summary>
/// JSON serialization context for NativeAOT-compatible serialization.
/// Uses source generators for trimming-safe JSON operations.
/// </summary>
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(RefreshTokenRequest))]
[JsonSerializable(typeof(ForgotPasswordRequest))]
[JsonSerializable(typeof(ResetPasswordRequest))]
[JsonSerializable(typeof(TwoFactorVerifyRequest))]
[JsonSerializable(typeof(ListResponse<SessionInfo>), TypeInfoPropertyName = "ListResponseSessionInfo")]
[JsonSerializable(typeof(ListResponse<ApiKeyInfo>), TypeInfoPropertyName = "ListResponseApiKeyInfo")]
[JsonSerializable(typeof(ListResponse<IamRole>), TypeInfoPropertyName = "ListResponseIamRole")]
[JsonSerializable(typeof(ListResponse<IamPermission>), TypeInfoPropertyName = "ListResponseIamPermission")]
[JsonSerializable(typeof(PagedResponse<IamUser>), TypeInfoPropertyName = "PagedResponseIamUser")]
[JsonSerializable(typeof(RevokeAllResponse))]
[JsonSerializable(typeof(SessionInfo))]
[JsonSerializable(typeof(ApiKeyInfo))]
[JsonSerializable(typeof(CreateApiKeyRequest))]
[JsonSerializable(typeof(CreateApiKeyResult))]
[JsonSerializable(typeof(TwoFactorSetupInfo))]
[JsonSerializable(typeof(EnableTwoFactorRequest))]
[JsonSerializable(typeof(EnableTwoFactorResponse))]
[JsonSerializable(typeof(DisableTwoFactorRequest))]
[JsonSerializable(typeof(RecoveryCodesResponse))]
[JsonSerializable(typeof(UserProfile))]
[JsonSerializable(typeof(ChangePasswordRequest))]
[JsonSerializable(typeof(IamUser))]
[JsonSerializable(typeof(UpdateUserRequest))]
[JsonSerializable(typeof(UserPermissions))]
[JsonSerializable(typeof(ResetUserPasswordRequest))]
[JsonSerializable(typeof(IamRole))]
[JsonSerializable(typeof(CreateRoleRequest))]
[JsonSerializable(typeof(UpdateRoleRequest))]
[JsonSerializable(typeof(AssignRoleRequest))]
[JsonSerializable(typeof(UnassignRoleRequest))]
[JsonSerializable(typeof(IamPermission))]
[JsonSerializable(typeof(GrantPermissionRequest))]
[JsonSerializable(typeof(RevokePermissionRequest))]
[JsonSerializable(typeof(ScopeTemplateInfo))]
[JsonSerializable(typeof(ScopeTemplateRequest))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<Guid>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, System.Text.Json.JsonElement>), TypeInfoPropertyName = "DictionaryStringJsonElement")]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}

// ====================================================================
// Authentication DTOs for JSON serialization
// ====================================================================

/// <summary>Token response from auth endpoints.</summary>
public sealed class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

/// <summary>Error response from API.</summary>
public sealed class ErrorResponse
{
    public string? Message { get; set; }
    public string? Detail { get; set; }
    public string? Title { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public string? TwoFactorToken { get; set; }
}

/// <summary>Login request DTO.</summary>
public sealed class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>Register request DTO.</summary>
public sealed class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Refresh token request DTO.</summary>
public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>Forgot password request DTO.</summary>
public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>Reset password request DTO.</summary>
public sealed class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Two-factor verification request DTO.</summary>
public sealed class TwoFactorVerifyRequest
{
    public string Code { get; set; } = string.Empty;
    public string TwoFactorToken { get; set; } = string.Empty;
}

/// <summary>Generic list response wrapper.</summary>
public sealed class ListResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>Generic paged response wrapper.</summary>
public sealed class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>Revoke all sessions response.</summary>
public sealed class RevokeAllResponse
{
    public int RevokedCount { get; set; }
}

/// <summary>Create API key request DTO.</summary>
public sealed class CreateApiKeyRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>Enable 2FA request DTO.</summary>
public sealed class EnableTwoFactorRequest
{
    public string VerificationCode { get; set; } = string.Empty;
}

/// <summary>Enable 2FA response DTO.</summary>
public sealed class EnableTwoFactorResponse
{
    public List<string> RecoveryCodes { get; set; } = new();
}

/// <summary>Disable 2FA request DTO.</summary>
public sealed class DisableTwoFactorRequest
{
    public string Password { get; set; } = string.Empty;
}

/// <summary>Recovery codes response DTO.</summary>
public sealed class RecoveryCodesResponse
{
    public List<string> RecoveryCodes { get; set; } = new();
}

/// <summary>Change password request DTO.</summary>
public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Reset user password request DTO (admin).</summary>
public sealed class ResetUserPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}
