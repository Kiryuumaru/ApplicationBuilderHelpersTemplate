---
applyTo: '**'
---
# Documentation Reference Rules

## Check Docs First

When you encounter something unfamiliar or need context about the codebase, **always check the documentation first** before making assumptions or asking for clarification.

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

## Feature Documentation

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

## When to Check Docs

- Before implementing a new feature → Check if similar patterns exist
- Before modifying an endpoint → Check current contract in feature doc
- Before writing tests → Check test architecture doc for conventions
- Before asking "how does X work?" → Read the relevant doc first
- When unsure about request/response format → Check feature doc examples
- When unsure about architecture patterns → Check architecture docs

## Documentation Index

Start with `docs/index.md` for a complete index of all documentation.
