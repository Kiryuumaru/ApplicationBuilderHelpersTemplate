---
applyTo: '**'
---
# File Structure Rules

## One Type Per File

Each file contains exactly one public type. File name matches the type name.

| Type Kind | File Name Format | Example |
|-----------|------------------|---------|
| Class | `{ClassName}.cs` | `UserService.cs` |
| Interface | `I{Name}.cs` | `IUserService.cs` |
| Record | `{RecordName}.cs` | `LoginRequest.cs` |
| Enum | `{EnumName}.cs` | `OrderStatus.cs` |

**Exceptions:**
- Private nested types within the containing type's file
- File-scoped types using `file` modifier

---

## Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes, Records, Structs | PascalCase | `UserService`, `LoginRequest` |
| Interfaces | `I` + PascalCase | `IUserService`, `ITokenProvider` |
| Methods | PascalCase | `GetUserAsync`, `ValidateToken` |
| Properties | PascalCase | `UserId`, `IsAuthenticated` |
| Private fields | `_camelCase` | `_userService`, `_logger` |
| Local variables | camelCase | `userId`, `tokenResult` |
| Constants | PascalCase | `DefaultTimeout`, `MaxRetries` |
| Parameters | camelCase | `userId`, `cancellationToken` |
| Async methods | Suffix `Async` | `GetUserAsync`, `SaveAsync` |

---

## Folder Structure

### Domain Layer

```
Domain/
└── {Feature}/
    ├── Entities/
    ├── ValueObjects/
    ├── Enums/
    ├── Events/
    └── Exceptions/
```

### Application Layer

```
Application/
└── {Feature}/
    ├── Interfaces/
    ├── Services/
    ├── Models/
    ├── Validators/
    └── Extensions/
```

### Infrastructure Layer

```
Infrastructure.{Provider}/
├── Services/
├── Repositories/
├── Configurations/
├── Extensions/
└── Models/
```

### Presentation Layer

```
Presentation.WebApi/
├── Controllers/V{n}/
├── Models/
│   ├── Requests/
│   └── Responses/
├── Middleware/
└── Filters/

Presentation.WebApp/
├── Components/
│   ├── Layout/
│   ├── Pages/
│   └── Shared/
├── Services/
└── Models/
```

---

## Member Ordering Within Files

1. Constants
2. Static fields
3. Instance fields
4. Constructors
5. Properties
6. Public methods
7. Private methods

---

## Namespace Convention

Namespace mirrors folder path from `src/`.

```
src/Application/Identity/Services/UserService.cs
→ namespace Application.Identity.Services;
```

---

## Prohibited Patterns

- NEVER have multiple public types in one file
- NEVER define DTOs inside controller files
- NEVER use nested public types
- NEVER leave empty placeholder/stub files after refactoring
- NEVER use file names that do not match the contained type
