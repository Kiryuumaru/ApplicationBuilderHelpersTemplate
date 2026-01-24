---
applyTo: '**/*Tests.cs'
---
# UI Test Practices

## Core Principle: Real User Interactions Only

**ALL UI TESTS MUST USE MOUSE CLICK + KEYBOARD TYPE ONLY**

Like a real person:
- Navigate by clicking links/buttons
- Fill forms by typing
- Submit by clicking buttons
- NO direct API calls from tests
- NO manual header injection
- NO bypassing the client-side auth flow

## Why This Matters

Tests that "cheat" by calling APIs directly with manually-set `Authorization` headers give false confidence. They pass while the actual user experience is broken.

**The goal**: If a test passes, a real user doing the same actions will succeed.

---

## Forbidden Patterns (Cheating)

### ❌ NEVER DO:

1. **Direct API Calls from Tests**
   ```csharp
   // BAD: Bypasses entire UI and auth flow
   await HttpClient.PostAsJsonAsync("/api/v1/auth/login", request);
   var response = await HttpClient.GetAsync("/api/v1/account/profile");
   ```

2. **Manual Authorization Headers**
   ```csharp
   // BAD: Bypasses token storage/retrieval
   request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
   HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
   ```

3. **API-Based Test Setup**
   ```csharp
   // BAD: Registering/logging in via API instead of UI
   await RegisterAndLoginViaApiAsync(username, email, password);
   var tokens = await GetTokensViaApiAsync(email, password);
   ```

4. **Arbitrary Delays**
   ```csharp
   // BAD: Flaky and slow
   await Task.Delay(2000);
   ```

5. **Exposing HttpClient to Tests**
   ```csharp
   // BAD: Test base class should NOT provide API access
   protected HttpClient HttpClient => _fixture.HttpClient;
   ```

---

## Required Patterns (Real User Interactions)

### ✅ ALWAYS DO:

1. **Use UI for All Actions**
   ```csharp
   // GOOD: Real user interaction
   await Page.Locator("#email").FillAsync(email);
   await Page.Locator("#password").FillAsync(password);
   await Page.Locator("button[type='submit']").ClickAsync();
   ```

2. **Navigate via UI**
   ```csharp
   // GOOD: Click links like a user
   await Page.Locator("button:has(.rounded-full)").ClickAsync(); // User menu
   await Page.GetByText("Profile").ClickAsync();
   ```

3. **Proper Wait Strategies**
   ```csharp
   // GOOD: Wait for specific conditions
   await Page.WaitForURLAsync(url => url.Contains("/dashboard"));
   await Page.Locator("[data-testid='profile-username']").WaitForAsync();
   await Page.Locator("[data-testid='loading-spinner']").WaitForAsync(
       new() { State = WaitForSelectorState.Hidden });
   ```

4. **Verify via UI State**
   ```csharp
   // GOOD: Check what's visible on screen
   var username = await Page.Locator("[data-testid='profile-username']").TextContentAsync();
   Assert.Equal(expectedUsername, username);
   
   var errorMessage = await Page.Locator("[data-testid='error-message']").IsVisibleAsync();
   Assert.False(errorMessage);
   ```

---

## Test Helper Guidelines

### WebAppTestBase.cs Helpers

All test base helpers must use ONLY Playwright UI interactions:

```csharp
// GOOD: Registration via UI
protected async Task<bool> RegisterUserAsync(string username, string email, string password)
{
    await GoToRegisterAsync();
    await Page.Locator("#username").FillAsync(username);
    await Page.Locator("#email").FillAsync(email);
    await Page.Locator("#password").FillAsync(password);
    await Page.Locator("#confirmPassword").FillAsync(password);
    await Page.Locator("#terms").CheckAsync();
    await Page.Locator("button[type='submit']").ClickAsync();
    await WaitForNavigationAwayFrom("/auth/register");
    return !Page.Url.Contains("/auth/register");
}

// GOOD: Login via UI
protected async Task<bool> LoginAsync(string email, string password)
{
    await GoToLoginAsync();
    await Page.Locator("#email").FillAsync(email);
    await Page.Locator("#password").FillAsync(password);
    await Page.Locator("button[type='submit']").ClickAsync();
    await WaitForNavigationAwayFrom("/auth/login");
    return !Page.Url.Contains("/auth/login");
}

// GOOD: Logout via UI
protected async Task LogoutAsync()
{
    await Page.Locator("button:has(.rounded-full)").ClickAsync();
    await Page.GetByText("Sign out").ClickAsync();
    await WaitForNavigationTo("/auth/login");
}
```

---

## Element Selection Strategy

### Preferred Selectors (in order)

1. **data-testid attributes** (most reliable)
   ```csharp
   await Page.Locator("[data-testid='submit-login']").ClickAsync();
   ```

2. **Form element IDs** (for inputs)
   ```csharp
   await Page.Locator("#email").FillAsync(email);
   await Page.Locator("#password").FillAsync(password);
   ```

3. **Semantic selectors** (for buttons/links)
   ```csharp
   await Page.Locator("button[type='submit']").ClickAsync();
   await Page.GetByRole(AriaRole.Link, new() { Name = "Profile" }).ClickAsync();
   ```

4. **Text content** (when unique)
   ```csharp
   await Page.GetByText("Sign out").ClickAsync();
   ```

### UI Elements That Need data-testid

Add `data-testid` attributes to these elements:

```html
<!-- Navigation -->
<button data-testid="user-menu">...</button>
<a data-testid="nav-profile">Profile</a>
<button data-testid="sign-out">Sign out</button>

<!-- Forms -->
<form data-testid="login-form">...</form>
<button data-testid="submit-login">Sign in</button>

<!-- Feedback -->
<div data-testid="success-message">...</div>
<div data-testid="error-message">...</div>
<div data-testid="loading-spinner">...</div>

<!-- Profile -->
<div data-testid="profile-username">...</div>
<div data-testid="profile-email">...</div>
```

---

## Test Categories

### Critical Auth Flow Tests

| Test | What It Validates |
|------|-------------------|
| `Register_Success` | User can register via UI |
| `Login_Success` | User can login via UI |
| `Login_Then_Profile` | After login, profile page loads (no 401) |
| `Login_Persist_Refresh` | Session survives page refresh |
| `Logout_Success` | User can logout via UI |

### Journey Tests (Full User Flows)

| Test | What It Validates |
|------|-------------------|
| `Journey_SignUp_To_Profile` | Register → Auto-login → Profile → See data |
| `Journey_Login_Logout_Login` | Complete login cycle works |
| `Journey_PasswordChange_Cycle` | Password change affects future logins |

---

## Success Criteria

Before committing UI tests, verify:

- [ ] Zero `HttpClient` usage in test code
- [ ] Zero `Authorization` header manipulation
- [ ] All actions use `ClickAsync()`, `FillAsync()`, `TypeAsync()`
- [ ] Wait strategies use conditions, not `Task.Delay()`
- [ ] Critical flow works: Login → Navigate to Profile → See data (no 401)
- [ ] Tests would pass if run manually by a human doing same actions

---

## Pre-Commit Checklist for UI Tests

```powershell
# Search for forbidden patterns
# If any matches found, fix before committing

# Check for HttpClient usage
grep -r "HttpClient" tests/**/

# Check for header manipulation
grep -r "Authorization.*Bearer" tests/**/

# Check for API calls
grep -r "PostAsJsonAsync\|GetAsync\|PutAsJsonAsync" tests/**/

# Check for arbitrary delays
grep -r "Task\.Delay" tests/**/
```

---

## The Standard

**If a test passes but a real user doing the exact same steps would fail, the test is worthless.**

UI tests exist to validate the user experience. They must exercise the same code paths a real user would trigger - including authentication flows, token storage, and API authorization.
