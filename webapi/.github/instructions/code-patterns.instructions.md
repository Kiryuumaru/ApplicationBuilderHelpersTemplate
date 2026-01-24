---
applyTo: '**'
---
# Code Patterns Rules

## Search Before Create

Before creating any type, utility, or pattern:

1. MUST search the codebase for existing types with similar purpose
2. MUST check `Application/{Feature}/Models/` and `Domain/{Feature}/ValueObjects/`
3. If found: MUST use it or extend it
4. If not found: MUST create in the appropriate shared location

---

## One Concept, One Type, One Location

- If same type defined in multiple files, MUST keep one definition and delete others
- If same concept has different names, MUST consolidate to single canonical name
- If private type could be shared, MUST move to appropriate shared location
- If anonymous type used for known concept, MUST use the existing named type

---

## No Duplication

MUST extract when:
- Same logic appears 2+ times
- Same pattern emerges across files
- Same constant value used in multiple locations
- Same error handling repeated

Shared code locations:
- Domain logic MUST go in `Domain/Shared/` or `Domain/{Feature}/`
- Application utilities MUST go in `Application/Shared/` or `Application/{Feature}/Extensions/`
- Test helpers MUST go in base test class or `TestHelpers/`
- Infrastructure utilities MUST go in `Infrastructure.{Provider}/Extensions/`

---

## Constants Over Magic Values

Every literal value used more than once MUST be a named constant.

- Domain constants MUST be in `Domain/{Feature}/Constants/`
- Application settings MUST be in Configuration or `Application/{Feature}/Constants/`
- Test values MUST be in test base class or constants file

---

## Consolidation Workflow

When duplicates are discovered:

1. MUST identify canonical location (prefer `Application/Models` or `Domain/ValueObjects`)
2. MUST keep the most complete definition
3. MUST update all references to use the shared type
4. MUST delete duplicate definitions
5. MUST verify build succeeds

---

## Prohibited Patterns

- NEVER copy-paste with minor variations
- NEVER use inline magic values (hardcoded strings, numbers, timeouts)
- NEVER duplicate validation logic
- NEVER repeat error handling patterns
- NEVER use anonymous types when named types exist
