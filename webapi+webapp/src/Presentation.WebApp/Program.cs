using Application.Client.Authentication.Interfaces;
using Application.Client.Authentication.Services;
using Application.Client.Iam.Interfaces;
using Application.Client.Iam.Services;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Presentation.WebApp;
using Presentation.WebApp.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API base address configuration
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? builder.HostEnvironment.BaseAddress;

// Blazored LocalStorage for token persistence
builder.Services.AddBlazoredLocalStorage();

// Token storage using LocalStorage
builder.Services.AddScoped<ITokenStorage, LocalStorageTokenStorage>();

// Auth state provider
builder.Services.AddScoped<IAuthStateProvider, ClientAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider, BlazorAuthStateProvider>();

// Authentication client (for login/register endpoints - no token needed)
builder.Services.AddHttpClient<IAuthenticationClient, AuthenticationClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
});

// Token refresh handler for authenticated requests
builder.Services.AddTransient<TokenRefreshHandler>();

// Authenticated HTTP client for API calls
builder.Services.AddHttpClient("API", client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
}).AddHttpMessageHandler<TokenRefreshHandler>();

// Factory for creating authenticated HttpClient
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("API"));

// Auth-related clients (sessions, API keys, 2FA, profile)
builder.Services.AddHttpClient<ISessionsClient, SessionsClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
}).AddHttpMessageHandler<TokenRefreshHandler>();

builder.Services.AddHttpClient<IApiKeysClient, ApiKeysClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
}).AddHttpMessageHandler<TokenRefreshHandler>();

builder.Services.AddHttpClient<ITwoFactorClient, TwoFactorClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
}).AddHttpMessageHandler<TokenRefreshHandler>();

builder.Services.AddHttpClient<IUserProfileClient, UserProfileClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
}).AddHttpMessageHandler<TokenRefreshHandler>();

// IAM clients (users, roles, permissions)
builder.Services.AddHttpClient<IUsersClient, UsersClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
}).AddHttpMessageHandler<TokenRefreshHandler>();

builder.Services.AddHttpClient<IRolesClient, RolesClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
}).AddHttpMessageHandler<TokenRefreshHandler>();

builder.Services.AddHttpClient<IPermissionsClient, PermissionsClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
}).AddHttpMessageHandler<TokenRefreshHandler>();

// Authorization services
builder.Services.AddAuthorizationCore();

var host = builder.Build();

// Initialize authentication state from stored tokens
var authStateProvider = host.Services.GetRequiredService<IAuthStateProvider>();
await authStateProvider.InitializeAsync();

await host.RunAsync();
