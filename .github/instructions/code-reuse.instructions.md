---
applyTo: '**'
---
# Code Reuse and Consistency Rules

## Core Principle: Search Before You Create

**Before writing any new type, utility, or pattern - search the codebase first.** If something similar already exists, use it or extend it. Never create duplicates.

## Required Workflow

### Before Creating a New Type

1. **Search for existing types** with similar purpose
2. **Check Application/Models, Domain/ValueObjects, Domain/Entities** for shared types
3. **Check if the same concept exists** under a different name
4. **If found**: Use the existing type, or refactor it to be more general
5. **If not found**: Create it in the appropriate shared location

### Before Creating a New Utility/Helper

1. **Search for existing utilities** that solve the same problem
2. **Check extension methods** in relevant namespaces
3. **If found**: Use it or extend it
4. **If not found**: Create it in a shared location others can use

## Forbidden Patterns

### ❌ NEVER DO:

1. **Duplicate Type Definitions**
   ```csharp
   // BAD: Same type defined in multiple places
   // In File1.cs
   private record struct SymbolInfo(string BaseAsset, string QuoteAsset);
   
   // In File2.cs  
   private record SymbolInfo(string BaseAsset, string QuoteAsset);
   
   // In File3.cs
   internal record SymbolInfo(string Base, string Quote);
   ```

2. **Similar Types with Different Names**
   ```csharp
   // BAD: Same concept, different names
   public record TradingPairInfo(string Base, string Quote);
   public record SymbolInfo(string BaseAsset, string QuoteAsset);
   public record AssetPair(string BaseAsset, string QuoteAsset);
   ```

3. **Reinventing Existing Patterns**
   ```csharp
   // BAD: Creating a new way to do something that already exists
   // When SymbolInfo already exists in Application.Trading.Models
   var pair = new { Base = "BTC", Quote = "USDT" }; // Anonymous type instead of SymbolInfo
   ```

4. **Local Types That Should Be Shared**
   ```csharp
   // BAD: Private nested type that could be reused
   private class OrderResult { public bool Success; public string Error; }
   // When Application layer already has a Result<T> or similar pattern
   ```

## Required Patterns

### ✅ ALWAYS DO:

1. **Use Existing Shared Types**
   ```csharp
   // GOOD: Using the shared SymbolInfo from Application.Trading.Models
   using Application.Trading.Models;
   
   var info = new SymbolInfo("BTC", "USDT");
   ```

2. **Reference Common Locations**
   ```csharp
   // GOOD: Check these locations before creating new types
   // - Application/{Feature}/Models/ - DTOs and shared models
   // - Domain/{Feature}/ValueObjects/ - Domain value types
   // - Domain/{Feature}/Entities/ - Domain entities
   // - Domain/Shared/ - Cross-cutting domain types
   ```

3. **Extend Rather Than Duplicate**
   ```csharp
   // GOOD: If existing type is close but not exact, extend or generalize it
   // Instead of creating PriceSymbolInfo, add to existing SymbolInfo or create derived type
   ```

4. **Consolidate When You Find Duplicates**
   ```csharp
   // GOOD: When you discover duplicates, consolidate them
   // 1. Pick the best location (usually Application or Domain)
   // 2. Keep one definition
   // 3. Update all references to use the shared type
   ```

## Search Commands

Before creating new code, use these searches:

```
# Search for similar type names
grep -r "record.*YourTypeName" src/
grep -r "class.*YourTypeName" src/

# Search for similar field combinations
grep -r "BaseAsset.*QuoteAsset" src/
grep -r "string baseAsset, string quoteAsset" src/

# Search in common model locations
ls src/Application/*/Models/
ls src/Domain/*/ValueObjects/
```

## Consolidation Checklist

When you find duplicates:

- [ ] Identify the canonical location (prefer Application/Models or Domain/ValueObjects)
- [ ] Keep the most complete/correct definition
- [ ] Update all usages to reference the shared type
- [ ] Remove duplicate definitions
- [ ] Ensure proper `using` statements are added

## The Standard

**One concept = One type = One location.**

If you find yourself writing a type that "seems familiar," stop and search. The few minutes spent searching saves hours of future refactoring and prevents bugs from inconsistent implementations.
