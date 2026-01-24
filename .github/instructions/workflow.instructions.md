---
applyTo: '**'
---
# Workflow Rules

## Fix Hygiene

If a fix attempt fails, undo all changes from that attempt before trying a different approach.

**Workflow:**
1. Apply fix
2. Verify fix (build, test)
3. If failed: undo changes completely
4. Apply next fix from clean state

**Prohibited Patterns:**
- NEVER apply fix B on top of failed fix A
- NEVER leave partial changes from failed attempts
- NEVER comment out failed code instead of removing
- NEVER proceed when current fix is broken

---

## Pre-Commit Verification

Before every commit:

| Check | Command | Required Result |
|-------|---------|-----------------|
| Build | `dotnet build` | 0 warnings, 0 errors |
| Tests | `dotnet test` | 100% pass |
| Nullable | Manual review | No `!` without justification |
| Warnings | Manual review | No `#pragma warning disable` |
| Patches | Manual review | No `// TODO`, `// HACK`, `// FIXME` |

---

## Architecture Review

Before committing, verify:

- MUST verify Domain layer has zero external package references
- MUST verify Application layer references only Domain
- MUST verify Infrastructure implements Application interfaces
- MUST verify Presentation injects interfaces, not concrete types
- MUST verify no `using Infrastructure.*` in Application layer
- MUST verify no framework attributes in Domain entities

---

## Documentation Sync

When modifying code:

| Change Type | Required Documentation Update |
|-------------|-------------------------------|
| New API endpoint | Feature doc in `docs/features/` |
| Changed request/response | Update examples in feature doc |
| New/removed tests | Update test list and counts |
| Architecture change | Update `docs/architecture/` |
| TODO completion | Update `TODO.md` |
