---
applyTo: '**'
---
# Code Style and Structure Consistency

## Core Principle: Match What Exists

**When adding new code, match the style and structure of existing code in the same area.** Consistency within a codebase is more important than personal preferences.

## Naming Conventions

### Types
| Type | Convention | Example |
|------|------------|---------|
| Classes/Records/Structs | PascalCase | `UserService`, `LoginRequest` |
| Interfaces | `I` + PascalCase | `IUserService`, `ITokenProvider` |
| Methods | PascalCase | `GetUserAsync`, `ValidateToken` |
| Properties | PascalCase | `UserId`, `IsAuthenticated` |
| Private fields | `_camelCase` | `_userService`, `_logger` |
| Local variables | camelCase | `userId`, `tokenResult` |
| Constants | PascalCase | `DefaultTimeout`, `MaxRetries` |
| Parameters | camelCase | `userId`, `cancellationToken` |

### Async Methods
- Suffix with `Async`: `GetUserAsync`, `ValidateTokenAsync`
- Exception: Interface methods where it's obvious (e.g., `Task<T>` return)

### File Names
- Match the primary type name: `UserService.cs` contains `class UserService`
- One public type per file (per file-organization rules)

## Folder Structure Patterns

### When Adding a New Feature
1. **Look at existing features** in the same layer
2. **Mirror the folder structure** of similar features
3. **Use the same subfolder names** (Interfaces/, Services/, Models/, etc.)

```
Application/
├── Identity/           ← Existing feature
│   ├── Interfaces/
│   ├── Services/
│   └── Models/
└── NewFeature/         ← New feature follows same pattern
    ├── Interfaces/
    ├── Services/
    └── Models/
```

### Forbidden Patterns
❌ Different structures for similar features
❌ Inventing new folder names when existing ones apply
❌ Mixing styles within the same layer

## Code Organization Within Files

### Using Statements
1. System namespaces first
2. Third-party namespaces
3. Project namespaces (alphabetical)
4. No unused usings

### Class Member Order
1. Constants
2. Static fields
3. Instance fields
4. Constructors
5. Properties
6. Public methods
7. Private methods

### Primary Constructor Pattern
When using primary constructors, prefer dependency injection parameters:
```csharp
public sealed class UserService(
    IUserRepository userRepository,
    ILogger<UserService> logger) : IUserService
```

## Before Adding New Code

1. **Find similar code** in the same layer/feature
2. **Match its style** exactly
3. **If no similar code exists**, follow the closest pattern in the codebase
4. **When in doubt**, check 2-3 examples and pick the most common pattern
