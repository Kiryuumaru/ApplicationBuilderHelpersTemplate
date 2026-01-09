---
applyTo: '**'
---
# Documentation Sync Rules

## Keep Docs in Sync with Code

When making code changes that affect documented features, **always update the corresponding documentation**.

## What Requires Doc Updates

### API Changes
- New endpoints → Add to feature doc
- Changed request/response schemas → Update examples
- Changed endpoint paths → Update all references
- New query parameters → Document in endpoint table
- Changed authentication requirements → Update auth notes

### Test Changes
- New tests → Update test count in feature doc
- Renamed tests → Update test list
- Removed tests → Remove from test list

### Architecture Changes
- New interfaces → Update architecture docs
- Changed patterns → Update architecture docs
- New infrastructure implementations → Document behavior differences

## Documentation Locations

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

## Checklist Before Commit

When modifying code, ask:

- [ ] Does this change any API endpoint? → Update feature doc
- [ ] Does this change request/response format? → Update examples
- [ ] Does this add/remove/rename tests? → Update test list and counts
- [ ] Does this change architecture patterns? → Update architecture doc
- [ ] Does this affect the TODO.md status? → Update TODO.md

## Example

If you add a new endpoint `POST /api/v1/auth/change-password`:

1. Add endpoint to table in `docs/features/authentication.md`
2. Add request/response example
3. Add tests to test coverage table
4. Update test count in `docs/features/README.md`
5. Mark TODO item as complete in `TODO.md`
