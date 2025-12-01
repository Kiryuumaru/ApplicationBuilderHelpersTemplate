# Test Project Restructure Plan - COMPLETED ✅

## Summary

The test project restructure has been completed. All 254 tests pass:
- Domain.UnitTests: 38 tests ✅
- Application.UnitTests: 21 tests ✅
- Application.IntegrationTests: 20 tests ✅ (persistence ignorant via DI)
- Presentation.FunctionalTests: 175 tests ✅

## 1. Project Renames - COMPLETED

| Current | New Name |
|---|---|
| `tests/Domain.Tests` | `tests/Domain.UnitTests` |
| `tests/Application.Tests` | `tests/Application.UnitTests` |
| `tests/Infrastructure.Tests` | `tests/Application.IntegrationTests` |
| `tests/Presentation.WebApp.Tests` | `tests/Presentation.FunctionalTests` |

## 2. Test Migration

| Test File | From | To | Reason |
|---|---|---|---|
| `Authorization/*` | Application.Tests | Application.UnitTests | Pure logic ✅ |
| `ConcurrentLocalStoreTests.cs` | Application.Tests | Application.IntegrationTests | Uses storage, better with real infra |
| `IdentityServiceTests.cs` | Application.Tests | Application.IntegrationTests | Uses real SQLite |
| `SqliteLocalStoreServiceTests.cs` | Infrastructure.Tests | Application.IntegrationTests | Refactor to use DI |

## 2.1 Add Tests for Shared/Common Code

| Layer | Path | Test In | What to Test |
|---|---|---|---|
| Domain | `Domain/Shared/Constants/` | Domain.UnitTests | Constant values |
| Domain | `Domain/Shared/Exceptions/` | Domain.UnitTests | Exception behavior |
| Domain | `Domain/Shared/Extensions/` | Domain.UnitTests | Extension methods |
| Domain | `Domain/Shared/Interfaces/` | Domain.UnitTests | (if testable) |
| Domain | `Domain/Shared/Models/` | Domain.UnitTests | Model validation |
| Domain | `Domain/Shared/Serialization/` | Domain.UnitTests | Serialization round-trips |
| Application | `Application/Common/Extensions/` | Application.UnitTests | Extension methods |
| Application | `Application/Common/Features/` | Application.UnitTests | Feature helpers |

## 3. Refactor Integration Tests for Persistence Ignorance

**Before:**
```csharp
using var service = new SqliteLocalStoreService(configuration);
await service.Open(ct);
```

**After:**
```csharp
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
new Application.Application().AddServices(services, configuration);
new EFCoreSqliteInfrastructure().AddServices(services, configuration);

using var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<ILocalStoreFactory>();
using var store = await factory.OpenStore("group", ct);
```

## 4. Project References

| Project | References |
|---|---|
| **Domain.UnitTests** | Domain |
| **Application.UnitTests** | Application, Domain |
| **Application.IntegrationTests** | Application, Domain, all Infrastructure.* |
| **Presentation.FunctionalTests** | Presentation.WebApp, Playwright |

## 5. Steps to Execute

1. Create `tests/Domain.UnitTests/` - move files from Domain.Tests
2. Create `tests/Application.UnitTests/` - Authorization tests only (pure logic)
3. Create `tests/Application.IntegrationTests/` - merge & refactor all infra-touching tests
4. Create `tests/Presentation.FunctionalTests/` - move from WebApp.Tests
5. Update solution file
6. Update `Build.cs` if needed
7. Delete old folders
8. Build and test

## 6. Final Structure

```
tests/
  Domain.UnitTests/
    Domain.UnitTests.csproj
    AppEnvironment/
    Authorization/
    Identity/
    Serialization/
    Shared/
      Constants/
      Exceptions/
      Extensions/
      Models/
    
  Application.UnitTests/
    Application.UnitTests.csproj
    Authorization/
      (pure logic tests only)
    Common/
      Extensions/
      Features/
      
  Application.IntegrationTests/
    Application.IntegrationTests.csproj
    Identity/
      IdentityServiceTests.cs
    LocalStore/
      LocalStoreIntegrationTests.cs
      ConcurrentLocalStoreTests.cs
      
  Presentation.FunctionalTests/
    Presentation.FunctionalTests.csproj
    (existing Playwright E2E tests)
```

## 7. Verification

- `.\build.ps1 clean` - clean artifacts
- `dotnet build` - compiles
- `dotnet test` - 385 tests pass
- Integration tests are infrastructure-agnostic (persistence ignorance)
- No mocking of infrastructure interfaces
- Shared helpers reduce duplication
