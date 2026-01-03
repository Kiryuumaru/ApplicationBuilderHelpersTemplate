---
applyTo: '**'
---
# Project Status

This project is **new and not yet released**. There are no production users or deployments.

## Breaking Changes Policy

- **Backward compatibility is NOT a concern** - we can freely make breaking changes
- Feel free to rename, restructure, or completely rewrite any code
- Database schemas, API contracts, and serialization formats can change without migration paths
- No deprecation warnings needed - just make the change directly

## Implications

When working on this codebase:
- Prioritize clean, correct design over compatibility
- Don't hesitate to fix naming mistakes or structural issues
- Refactoring is encouraged without worrying about existing consumers
- Test data and schemas can be reset/recreated as needed
