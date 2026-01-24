using System.Security.Claims;
using System.Text.Json;
using Application.Client.Authentication.Interfaces;
using Application.Client.Authentication.Models;
using Application.Client.Json;

namespace Application.Client.Authentication.Services;

/// <summary>
/// Scoped service that provides access to authentication state.
/// Uses a singleton AuthStateNotifier to share state across scopes,
/// allowing state changes from TokenRefreshHandler to be visible to UI.
/// </summary>
internal class ClientAuthStateProvider : IAuthStateProvider
{
    private readonly ITokenStorage _tokenStorage;
    private readonly AuthStateNotifier _notifier;

    public AuthState CurrentState => _notifier.CurrentState;
    public event Action? OnStateChanged
    {
        add => _notifier.OnStateChanged += value;
        remove => _notifier.OnStateChanged -= value;
    }

    public ClientAuthStateProvider(ITokenStorage tokenStorage, AuthStateNotifier notifier)
    {
        _tokenStorage = tokenStorage;
        _notifier = notifier;
    }

    public async Task InitializeAsync()
    {
        var credentials = await _tokenStorage.GetCredentialsAsync();
        if (credentials != null && credentials.IsValid)
        {
            _notifier.SetState(BuildAuthState(credentials));
        }
        else
        {
            _notifier.ClearState();
        }
    }

    public async Task UpdateStateAsync(StoredCredentials credentials)
    {
        await _tokenStorage.StoreCredentialsAsync(credentials);
        _notifier.SetState(BuildAuthState(credentials));
    }

    public async Task ClearStateAsync()
    {
        await _tokenStorage.ClearCredentialsAsync();
        _notifier.ClearState();
    }

    private static AuthState BuildAuthState(StoredCredentials credentials)
    {
        try
        {
            var claims = ParseClaimsFromJwt(credentials.AccessToken);
            
            var userIdStr = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;
            var username = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "name")?.Value;
            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == "email")?.Value;
            var twoFactorEnabled = claims.Any(c => c.Type == "amr" && c.Value == "mfa") ||
                                   claims.Any(c => c.Type == "2fa_enabled" && c.Value == "true");

            var userId = Guid.TryParse(userIdStr, out var parsedId) ? parsedId : Guid.Empty;

            return new AuthState
            {
                IsAuthenticated = true,
                UserId = userId,
                Username = username,
                Email = email,
                Roles = credentials.Roles,
                Permissions = credentials.Permissions,
                TokenExpiry = credentials.AccessTokenExpiry,
                TwoFactorEnabled = twoFactorEnabled
            };
        }
        catch
        {
            return AuthState.Anonymous;
        }
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize(
            jsonBytes,
            AppJsonSerializerContext.Default.DictionaryStringJsonElement);

        if (keyValuePairs == null)
            return claims;

        foreach (var kvp in keyValuePairs)
        {
            var element = kvp.Value;
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    claims.Add(new Claim(kvp.Key, item.GetString() ?? ""));
                }
            }
            else
            {
                claims.Add(new Claim(kvp.Key, element.ToString()));
            }
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64.Replace('-', '+').Replace('_', '/'));
    }
}
