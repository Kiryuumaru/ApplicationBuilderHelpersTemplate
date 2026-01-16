using Blazored.LocalStorage;
using Microsoft.JSInterop;
using Presentation.WebApp.Client.Components.Theme.Interfaces;

namespace Presentation.WebApp.Client.Components.Theme.Services;

internal sealed class ThemeService : IThemeService
{
    private const string ThemeStorageKey = "theme_preference";
    private const string DarkClass = "dark";

    private readonly IJSRuntime _jsRuntime;
    private readonly ILocalStorageService _localStorage;

    private bool? _cachedIsDark;

    public event Action<bool>? ThemeChanged;

    public ThemeService(IJSRuntime jsRuntime, ILocalStorageService localStorage)
    {
        _jsRuntime = jsRuntime;
        _localStorage = localStorage;
    }

    public async Task<bool> IsDarkModeAsync()
    {
        if (_cachedIsDark.HasValue)
        {
            return _cachedIsDark.Value;
        }

        _cachedIsDark = await _jsRuntime.InvokeAsync<bool>(
            "eval",
            $"document.documentElement.classList.contains('{DarkClass}')"
        );

        return _cachedIsDark.Value;
    }

    public async Task SetDarkModeAsync(bool isDark)
    {
        var action = isDark ? "add" : "remove";
        await _jsRuntime.InvokeVoidAsync(
            "eval",
            $"document.documentElement.classList.{action}('{DarkClass}')"
        );

        _cachedIsDark = isDark;
        await _localStorage.SetItemAsync(ThemeStorageKey, isDark);

        ThemeChanged?.Invoke(isDark);
    }

    public async Task<bool> ToggleDarkModeAsync()
    {
        var isDark = await IsDarkModeAsync();
        var newValue = !isDark;
        await SetDarkModeAsync(newValue);
        return newValue;
    }

    public async Task InitializeAsync()
    {
        bool isDark;

        try
        {
            var stored = await _localStorage.GetItemAsync<bool?>(ThemeStorageKey);
            if (stored.HasValue)
            {
                isDark = stored.Value;
            }
            else
            {
                // Fall back to system preference
                isDark = await _jsRuntime.InvokeAsync<bool>(
                    "eval",
                    "window.matchMedia('(prefers-color-scheme: dark)').matches"
                );
            }
        }
        catch
        {
            // Default to light mode if storage fails
            isDark = false;
        }

        await SetDarkModeAsync(isDark);
    }
}
