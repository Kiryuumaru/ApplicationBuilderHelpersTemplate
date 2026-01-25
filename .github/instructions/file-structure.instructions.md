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
├── Shared/
│   ├── Interfaces/
│   └── Extensions/
└── {Feature}/
    ├── Entities/
    ├── ValueObjects/
    ├── Enums/
    ├── Events/
    ├── Constants/
    ├── Services/
    └── Exceptions/
```

### Application Layer

```
Application/
├── Shared/
│   ├── Interfaces/
│   ├── Models/
│   └── Extensions/
└── {Feature}/
    ├── Ports/
    │   ├── In/
    │   └── Out/
    ├── Services/
    ├── Models/
    ├── Workers/
    ├── Validators/
    └── Extensions/
```

### Application.Server Layer

```
Application.Server/
├── Shared/
│   └── Interfaces/
└── {Feature}/
    ├── Ports/
    │   ├── In/
    │   └── Out/
    ├── Services/
    ├── Models/
    └── Workers/
```

### Application.Client Layer

```
Application.Client/
├── Shared/
│   └── Interfaces/
└── {Feature}/
    ├── Ports/
    │   ├── In/
    │   └── Out/
    ├── Services/
    └── Models/
```

### Infrastructure Layer

```
Infrastructure.{Provider}/
├── Adapters/
├── Services/
├── Repositories/
├── Configurations/
├── Extensions/
└── Models/

Infrastructure.{Provider}.{Feature}/
├── Adapters/
├── Repositories/
├── Configurations/
└── Extensions/
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

Presentation.WebApp.Server/
├── Workers/
├── Controllers/
└── Extensions/

Presentation.WebApp.Client/
├── Components/
│   ├── Layout/
│   ├── Pages/
│   └── Shared/
├── Services/
└── Models/
```

### Presentation Contracts

```
Presentation/
├── Contracts/
│   └── {Feature}/
│       ├── Requests/
│       ├── Responses/
│       └── Hubs/
└── Shared/
    ├── Components/
    └── Extensions/
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
- NEVER place a type in a folder that does not match its kind

---

## Type Placement by Kind

Files MUST be placed in folders matching their type kind.

| Type Kind | Required Folder | Example |
|-----------|-----------------|---------|
| Interface | `Interfaces/` | `Interfaces/IUserService.cs` |
| Port/In Interface | `Ports/In/` | `Ports/In/IOrderService.cs` |
| Port/Out Interface | `Ports/Out/` | `Ports/Out/IOrderRepository.cs` |
| Enum | `Enums/` | `Enums/OrderStatus.cs` |
| Record (DTO/Model) | `Models/` | `Models/LoginRequest.cs` |
| Service class | `Services/` | `Services/UserService.cs` |
| Adapter class | `Adapters/` | `Adapters/BinanceExchangeAdapter.cs` |
| Repository class | `Repositories/` | `Repositories/OrderRepository.cs` |
| Entity | `Entities/` | `Entities/User.cs` |
| Value object | `ValueObjects/` | `ValueObjects/Email.cs` |
| Exception | `Exceptions/` | `Exceptions/UserNotFoundException.cs` |
| Extension class | `Extensions/` | `Extensions/ServiceCollectionExtensions.cs` |
| Validator | `Validators/` | `Validators/LoginRequestValidator.cs` |
| Configuration | `Configurations/` | `Configurations/UserConfiguration.cs` |
| Constants class | `Constants/` | `Constants/ErrorMessages.cs` |
| Application Worker | `Workers/` | `Workers/TradeFiller.cs` |
| Presentation Worker Host | `Workers/` | `Workers/TradeFillerHost.cs` |

**Verification:**
- Before creating a file, identify its type kind
- Place in the corresponding folder within the feature/component area
- If folder does not exist, create it
