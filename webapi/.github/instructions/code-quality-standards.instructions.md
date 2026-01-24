---
applyTo: '**'
---
# Code Quality Standards: Zero Tolerance for Imperfection

## Core Philosophy

**Write code as if lives depend on it.** Every line you write could be running on a medical device that keeps patients alive, or on a spacecraft carrying astronauts to Mars. A single warning, a temporary patch, or a "good enough" fix could be the difference between mission success and catastrophic failure.

## Absolute Requirements

### Zero Warnings Policy

- **Every warning is a potential bug.** Treat warnings as errors.
- **Never ignore warnings.** Fix the root cause, not the symptom.
- **Never suppress warnings** unless you can prove mathematically that suppression is the only correct solution.
- Build must complete with **0 warnings, 0 errors** - no exceptions.

### No Temporary Patches

❌ **NEVER DO:**

```csharp
// TODO: Fix this later
// HACK: This works for now
// FIXME: Temporary workaround
var value = possiblyNull!;  // Silencing nullable warning
#pragma warning disable CS8602
```

✅ **ALWAYS DO:**

- Fix the actual problem, not the compiler message
- If something can be null, handle the null case properly
- If a warning appears, understand WHY and fix the underlying issue
- Design types and APIs so invalid states are unrepresentable

### Nullable Reference Types

The `!` (null-forgiving) operator is **almost always wrong**. It tells the compiler "trust me, this isn't null" - but if you're wrong, you get a runtime exception.

❌ **NEVER DO (except as absolute last resort):**

```csharp
string name = user.Name!;  // "Trust me" - DANGEROUS
var item = collection.FirstOrDefault()!;  // Could be null!
return _service!.GetData();  // Field might not be initialized
```

✅ **ALWAYS DO:**

```csharp
// Option 1: Proper null check
string name = user.Name ?? throw new InvalidOperationException("Name is required");

// Option 2: Handle the null case
var item = collection.FirstOrDefault();
if (item is null)
{
    return NotFound();
}

// Option 3: Use proper initialization
private readonly IService _service;
public MyClass(IService service)
{
    _service = service ?? throw new ArgumentNullException(nameof(service));
}

// Option 4: Make the type honestly nullable
public string? OptionalName { get; set; }
```

### When `!` Is Acceptable (Rare Cases)

Only use `!` when ALL of these conditions are met:

1. You have **proven** the value cannot be null at this point
2. The compiler cannot infer this due to analysis limitations
3. You have added a comment explaining **why** it's safe
4. There is no reasonable way to restructure the code

```csharp
// Acceptable: Debug.Assert guarantees non-null, but compiler can't see it
Debug.Assert(result != null, "Result is guaranteed by prior validation");
ProcessResult(result!); // Safe: Assert proves non-null

// Acceptable: Pattern match already proved non-null in scope
if (obj is string s && s.Length > 0)
{
    // s is definitely not null here, but sometimes compiler loses track
    UseString(s!);
}
```

## Reliability Standards

### 99% Is Failure

- 99% reliability = 1 failure per 100 operations
- 99.9% reliability = 1 failure per 1,000 operations  
- 99.99% reliability = 1 failure per 10,000 operations

**None of these are acceptable.** Aim for 100% reliability.

A Mars rover that fails 1% of the time will not complete its mission. A medical device that fails 0.1% of the time will kill patients. Code to the standard where failure is simply not an option.

### Defense in Depth

1. **Make illegal states unrepresentable** - Use the type system to prevent bugs
2. **Validate at boundaries** - Check all inputs at system edges
3. **Fail fast** - Throw exceptions early rather than propagating bad state
4. **Be explicit** - Never rely on implicit behavior or defaults

### Code Longevity

Write code that will work correctly for **decades**:

- Don't rely on current implementation details that might change
- Use stable, well-documented APIs
- Avoid clever tricks that future maintainers won't understand
- Document **why**, not just **what**

## Pre-Commit Checklist

Before committing ANY code, verify:

- [ ] `dotnet build` completes with **0 warnings**
- [ ] `dotnet test` passes with **100% success rate**
- [ ] No `!` operators added without documented justification
- [ ] No `#pragma warning disable` added
- [ ] No `// TODO`, `// HACK`, `// FIXME` comments added
- [ ] No suppression attributes (`[SuppressMessage]`) added
- [ ] All nullable types are intentionally nullable
- [ ] All non-nullable types are guaranteed non-null

---

## Fix Hygiene: Undo Before Retry

**If a fix attempt does not solve the problem, undo the changes from that specific fix before trying a different approach.**

### Why This Matters

Stacking unverified fixes creates cascading issues:
- Each failed fix introduces unintended side effects
- Subsequent fixes may mask root causes or introduce new bugs
- Debugging becomes exponentially harder as layers accumulate
- The codebase drifts further from a known-good state

### Required Workflow

1. **Apply fix** - Make changes to address the issue
2. **Verify fix** - Build, test, or otherwise confirm the fix works
3. **If fix fails** - Undo the changes from that fix before proceeding
4. **Then retry** - Apply a different fix starting from a clean state

### What "Undo" Means

- Undo file modifications introduced by the failed fix
- Remove any new files created for the failed fix
- Restore any code deleted by the failed fix
- Return to the state before that fix attempt began

### Forbidden Fix Patterns

❌ Applying fix B on top of failed fix A without undoing A  
❌ Leaving partial changes from a failed fix "in case they help"  
❌ Commenting out failed fix code instead of removing it  
❌ Proceeding with additional changes when the current fix is broken  

**Failed fix attempts leave no trace.**

---

## DRY: Don't Repeat Yourself

**If you write similar code twice, you should have extracted it the first time.**

### When to Extract

1. **Same logic appears 2+ times** - If you copy-paste, extract it
2. **Pattern emerges across files** - Similar try/catch, loops, conditionals
3. **Magic values repeat** - Timeouts, URLs, constants
4. **Error handling duplicated** - Same catch blocks, same fallback logic

### Where to Put Shared Code

| Code Type | Location |
|-----------|----------|
| Domain logic | `Domain/Shared/` or feature-specific folder |
| Application utilities | `Application/Shared/` or `Application/{Feature}/Extensions/` |
| Test helpers | Base test class or `TestHelpers/` folder |
| Infrastructure utilities | `Infrastructure.{Provider}/Extensions/` |

### Forbidden Patterns

❌ Copy-paste with minor variations  
❌ Inline magic values (hardcoded timeouts, strings, numbers)  
❌ Duplicated validation logic across methods  
❌ Same error handling pattern repeated  

### Required Patterns

✅ Extract repeated logic to helper methods  
✅ Use constants for magic values  
✅ Create base classes for shared behavior  
✅ Use extension methods for common operations  

```csharp
// BAD: Same timeout repeated everywhere
await element1.WaitForAsync(new() { Timeout = 10000 });
await element2.WaitForAsync(new() { Timeout = 10000 });

// GOOD: Single constant, reusable helper
protected const int DefaultTimeoutMs = 10_000;

protected async Task<ILocator> WaitForAsync(string selector)
{
    var locator = Page.Locator(selector);
    await locator.WaitForAsync(new() { Timeout = DefaultTimeoutMs });
    return locator;
}
```

Every duplication is a maintenance burden. Extract early, extract often.

---

## Service Implementation Accessibility

Consumers depend on abstractions (interfaces), not concrete implementations. Making implementations `internal` enforces this at compile time and keeps the public API surface clean.

### ❌ NEVER DO:

```csharp
public class ToastService : IToastService
public class IndexedDBLocalStoreService : ILocalStoreService
```

### ✅ ALWAYS DO:

```csharp
public interface IToastService { }
internal class ToastService : IToastService { }

public interface ILocalStoreService { }
internal sealed class IndexedDBLocalStoreService : ILocalStoreService { }
```

This pattern also eliminates CS1591 warnings (missing XML documentation) on implementations without needing to add XML docs - internal types don't require documentation.

---

## The Standard

**If you wouldn't trust this code with your life, don't commit it.**

Every shortcut you take, every warning you ignore, every "temporary" fix you leave behind - these are technical debt that compounds. In critical systems, that debt is paid in human lives.

Write perfect code. Accept nothing less.
