---
applyTo: '**'
---
# File Organization Rules

## Core Principle: One Type Per File

Each file should contain **exactly one** public type (class, record, struct, interface, or enum). This improves discoverability, reduces merge conflicts, and keeps files focused.

## File Naming

- **File name must match the type name** - `OrderService.cs` contains `class OrderService`
- **Use PascalCase** for file names matching the type name
- **Interfaces** start with `I` - `IOrderService.cs` contains `interface IOrderService`

## Folder Structure by Type Category

Organize files into folders based on their purpose within each project:

### Domain Layer Folders
```
Domain/
??? {Feature}/
?   ??? Entities/          # Aggregate roots and entities
?   ??? ValueObjects/      # Immutable value types
?   ??? Enums/             # Enumeration types
?   ??? Events/            # Domain events
?   ??? Exceptions/        # Domain-specific exceptions
```

### Application Layer Folders
```
Application/
??? {Feature}/
?   ??? Interfaces/        # Service contracts and abstractions
?   ??? Services/          # Application service implementations
?   ??? Models/            # DTOs, request/response models
?   ??? Validators/        # Input validation logic
?   ??? Extensions/        # Extension method classes
```

### Infrastructure Layer Folders
```
Infrastructure.{Provider}/
??? Services/              # Interface implementations
??? Repositories/          # Data access implementations
??? Configurations/        # EF Core configurations, options
??? Extensions/            # DI registration extensions
??? Models/                # Provider-specific models (e.g., API responses)
```

### Presentation Layer Folders
```
Presentation.WebApi/
??? Controllers/
?   ??? V{n}/              # Versioned controllers
??? Models/                # API request/response DTOs
?   ??? Requests/          # Input models
?   ??? Responses/         # Output models
?   ??? SchemaFilters/     # Swagger customizations
??? Attributes/            # Custom attributes
??? Middleware/            # HTTP pipeline middleware
??? Filters/               # Action/exception filters
??? ConfigureOptions/      # Options configuration

Presentation.WebApp/
??? Components/
?   ??? Layout/            # Layout components
?   ??? Pages/             # Routable page components
?   ??? Shared/            # Reusable UI components
??? Services/              # Client-side services
??? Models/                # View models
```

## Forbidden Patterns

### ? NEVER DO:

1. **Multiple Public Types in One File**
   ```csharp
   // BAD: OrderService.cs
   public class OrderService { }
   public class OrderValidator { }     // WRONG! Separate file
   public record OrderRequest { }      // WRONG! Separate file
   public enum OrderStatus { }         // WRONG! Separate file
   ```

2. **Response/Request DTOs in Controller Files**
   ```csharp
   // BAD: MarketsController.cs
   public class MarketsController { }
   public record PriceResponse { }     // WRONG! Move to Models/Responses/
   public record ExchangeInfo { }      // WRONG! Move to Models/Responses/
   ```

3. **Nested Public Types**
   ```csharp
   // BAD: Nesting public types
   public class OrderService
   {
       public class OrderResult { }    // WRONG! Separate file
   }
   ```

4. **Misplaced Files**
   ```csharp
   // BAD: Service in wrong folder
   // Services/OrderEntity.cs          // WRONG! Entities go in Entities/
   // Models/OrderService.cs           // WRONG! Services go in Services/
   ```

## Required Patterns

### ? ALWAYS DO:

1. **One Type Per File**
   ```
   Services/
   ??? OrderService.cs          # Contains only OrderService
   ??? OrderValidator.cs        # Contains only OrderValidator
   
   Models/
   ??? OrderRequest.cs          # Contains only OrderRequest
   ??? OrderResponse.cs         # Contains only OrderResponse
   ```

2. **Group Related Response Models**
   ```
   Controllers/V1/
   ??? MarketsController.cs     # Controller only
   
   Models/Responses/
   ??? ExchangeInfo.cs
   ??? ExchangeListResponse.cs
   ??? PriceResponse.cs
   ??? PriceInfo.cs
   ??? AllPricesResponse.cs
   ??? TradingPairInfo.cs
   ??? TradingPairsResponse.cs
   ??? CandleInfo.cs
   ??? CandlesResponse.cs
   ```

3. **Feature-Based Organization for Large Projects**
   ```
   Application/
   ??? Trading/
   ?   ??? Interfaces/
   ?   ?   ??? IOrderService.cs
   ?   ??? Services/
   ?   ?   ??? OrderService.cs
   ?   ??? Models/
   ?       ??? OrderResult.cs
   ??? Identity/
       ??? Interfaces/
       ?   ??? IUserService.cs
       ??? Services/
           ??? UserService.cs
   ```

## Exceptions (When Multiple Types Are Acceptable)

The following cases allow related types in the same file:

1. **Private nested types** - Helper classes used only by the containing type
2. **File-scoped types** using `file` modifier (C# 11+) - Internal implementation details
3. **Very tightly coupled small records** - Only when they form a single logical unit AND are under 10 lines total

```csharp
// ACCEPTABLE: Private nested type
public class OrderProcessor
{
    private class ProcessingContext { }  // OK - private helper
}

// ACCEPTABLE: File-scoped implementation detail
file class InternalHelper { }  // OK - not visible outside file
```

## Refactoring Checklist

When you see multiple public types in a file:

- [ ] Create a new file for each public type
- [ ] Name each file after its contained type
- [ ] Move the file to the appropriate folder based on type category
- [ ] Update any `using` statements if namespaces change
- [ ] Verify the solution builds successfully

## Namespace Conventions

Namespaces should mirror folder structure:

```csharp
// File: src/Presentation.WebApi/Models/Responses/PriceResponse.cs
namespace Presentation.WebApi.Models.Responses;

public record PriceResponse(...);
```

```csharp
// File: src/Application/Trading/Interfaces/IOrderService.cs
namespace Application.Trading.Interfaces;

public interface IOrderService { }
```
