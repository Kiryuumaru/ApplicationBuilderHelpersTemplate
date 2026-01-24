---
applyTo: '**'
---
# Code Quality Rules

## Zero Warnings

Build MUST complete with 0 warnings and 0 errors. Every warning is a potential bug.

---

## Nullable Handling

The null-forgiving operator (`!`) is prohibited except when ALL conditions are met:
1. Value is proven non-null at that point
2. Compiler cannot infer this due to analysis limitations
3. Comment explains why it is safe
4. No reasonable restructuring alternative exists

Nullable handling patterns:
- For possibly null value, USE `?? throw new InvalidOperationException()`
- For null check, USE `if (x is null) return;`
- For optional value, DECLARE as nullable type `T?`
- For constructor parameter, USE `?? throw new ArgumentNullException(nameof(param))`

---

## Prohibited Patterns

- NEVER use `// TODO:` comments
- NEVER use `// HACK:` comments
- NEVER use `// FIXME:` comments
- NEVER use `#pragma warning disable`
- NEVER use `[SuppressMessage]` attributes
- NEVER use `var x = value!;` without justification comment
- NEVER ignore compiler warnings
- NEVER suppress warnings instead of fixing root cause

---

## Reliability Principles

- MUST make illegal states unrepresentable using the type system
- MUST validate at boundaries by checking all inputs at system edges
- MUST fail fast by throwing early rather than propagating bad state
- MUST be explicit and never rely on implicit behavior or defaults

---

## Code Longevity

- MUST use stable, documented APIs
- MUST avoid implementation-specific tricks
- MUST document reasoning, not mechanics
