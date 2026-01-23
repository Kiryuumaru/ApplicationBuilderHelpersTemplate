---
applyTo: '**'
---
# Documentation Rules

## Core Principle: Check Docs First, Keep Docs Current

When you encounter something unfamiliar, **check the documentation first** before making assumptions. When you make changes, **update the documentation** to keep it in sync.

---

## Reading Documentation

### When to Check Docs

- Before implementing a new feature → Check if similar patterns exist
- Before modifying an endpoint → Check current contract in feature doc
- Before writing tests → Check test architecture doc for conventions
- Before asking "how does X work?" → Read the relevant doc first
- When unsure about request/response format → Check feature doc examples
- When unsure about architecture patterns → Check architecture docs

### Documentation Index

Start with `docs/index.md` for a complete index of all documentation.

---

## Documentation Locations

| Need to Know | Check |
|--------------|-------|
| API endpoints & contracts | `docs/features/*.md` |
| Architecture patterns | `docs/architecture/*.md` |
| Test setup & conventions | `docs/architecture/test-architecture.md` |
| Paper/Live account design | `docs/architecture/core-architecture.md` |
| Current implementation status | `TODO.md` |
| Build commands | `.github/instructions/build-commands.instructions.md` |
| File organization | `.github/instructions/file-organization.instructions.md` |
| Architecture rules | `.github/instructions/architecture.instructions.md` |

### Feature Documentation

| Feature | Location |
|---------|----------|
| Authentication | `docs/features/authentication.md` |
| Exchange Accounts | `docs/features/exchange-accounts.md` |
| Trading Orders | `docs/features/trading-orders.md` |
| SignalR Hubs | `docs/features/signalr-hubs.md` |
| Bot System | `docs/features/bot-system.md` |
| User Management | `docs/features/user-management.md` |
| Portfolio Analytics | `docs/features/portfolio-analytics.md` |
| Favorites | `docs/features/favorites.md` |
| Backtesting | `docs/features/backtesting.md` |
| Markets (Public) | `docs/features/markets.md` |

---

## Updating Documentation

### What Requires Doc Updates

#### API Changes
- New endpoints → Add to feature doc
- Changed request/response schemas → Update examples
- Changed endpoint paths → Update all references
- New query parameters → Document in endpoint table
- Changed authentication requirements → Update auth notes

#### Test Changes
- New tests → Update test count in feature doc
- Renamed tests → Update test list
- Removed tests → Remove from test list

#### Architecture Changes
- New interfaces → Update architecture docs
- Changed patterns → Update architecture docs
- New infrastructure implementations → Document behavior differences

### Documentation Update Locations

| Change Type | Update Location |
|-------------|-----------------|
| Auth API | `docs/features/authentication.md` |
| Accounts API | `docs/features/exchange-accounts.md` |
| Orders API | `docs/features/trading-orders.md` |
| Hubs | `docs/features/signalr-hubs.md` |
| Bot API | `docs/features/bot-system.md` |
| User API | `docs/features/user-management.md` |
| Portfolio API | `docs/features/portfolio-analytics.md` |
| Favorites API | `docs/features/favorites.md` |
| Backtests API | `docs/features/backtesting.md` |
| Markets API | `docs/features/markets.md` |
| Paper/Live architecture | `docs/architecture/core-architecture.md` |
| Test setup | `docs/architecture/test-architecture.md` |
| Test counts | `docs/index.md`, `docs/features/README.md` |

---

## Pre-Commit Checklist for Documentation

When modifying code, ask:

- [ ] Does this change any API endpoint? → Update feature doc
- [ ] Does this change request/response format? → Update examples
- [ ] Does this add/remove/rename tests? → Update test list and counts
- [ ] Does this change architecture patterns? → Update architecture doc
- [ ] Does this affect the TODO.md status? → Update TODO.md

### Example

If you add a new endpoint `POST /api/v1/auth/change-password`:

1. Add endpoint to table in `docs/features/authentication.md`
2. Add request/response example
3. Add tests to test coverage table
4. Update test count in `docs/features/README.md`
5. Mark TODO item as complete in `TODO.md`
