using Application.Client.Authentication.Models;

namespace Application.Client.Authentication.Services;

/// <summary>
/// Singleton that holds shared auth state and events.
/// Allows multiple scoped instances of ClientAuthStateProvider to share state
/// so that state changes in one scope (e.g., TokenRefreshHandler) are visible
/// to UI components in other scopes.
/// </summary>
internal sealed class AuthStateNotifier
{
    private AuthState _currentState = AuthState.Anonymous;

    public AuthState CurrentState => _currentState;

    public event Action? OnStateChanged;

    public void SetState(AuthState state)
    {
        _currentState = state;
        OnStateChanged?.Invoke();
    }

    public void ClearState()
    {
        _currentState = AuthState.Anonymous;
        OnStateChanged?.Invoke();
    }
}
