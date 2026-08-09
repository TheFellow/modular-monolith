# Semantic parity ledger

This ledger tracks observable parity with `/go-modular-monolith`. A checked item
requires production-shaped tests, not only a matching type or method name.

## Foundation

- [x] Prefixed, strongly typed IDs for drinks, ingredients, inventory, menus,
  orders, and audit entries
- [x] Typed application errors with safe messages and CLI/HTTP/gRPC mappings
- [x] Cursor paging primitives and complete traversal; authorized page filling
  remains part of the query pipeline slice
- [x] Money, currency, volume/discrete measurement, tags, and quality values
- [ ] Checked filter grammar, canonical AST, exact evaluator, and safe pushdowns
- [x] Shared SQLite store, initial migration, explicit session transaction, and rollback
- [ ] Fresh operation contexts, middleware order, telemetry, and auditing
- [ ] Cedar policies/entities through `cedar-dotnet`
- [ ] Generated two-phase, non-cascading event dispatcher

## Domains

- [ ] Ingredients CRUD, retirement, and permanent replacement
- [ ] Drinks recipes, review state, substitution, and lifecycle
- [ ] Inventory on-hand/reserved/available stock and adjustment reasons
- [ ] Menus curation, publication, availability, analytics, and readiness
- [ ] Orders placement, reservation, completion, cancellation, and blocking
- [ ] Append-only audit history, actor queries, and touched resources
- [ ] Polymorphic tags, registered target loaders, atomic tagged mutations, ABAC

## Stateful workflows

- [ ] Placement captures an immutable usage snapshot and reserves atomically
- [ ] Completion consumes reservations; cancellation releases them
- [ ] Shortage blocks every affected pending order; restock unblocks it
- [ ] Retirement rewrites only compatible explicit replacements
- [ ] Required retired references cause review; optional references disappear
- [ ] Accepted order snapshots remain historical truth
- [ ] Published menus degrade honestly; drafts with blockers cannot publish
- [ ] Event handler order cannot change outcomes and any failure rolls back all

## Surfaces

- [ ] CLI command/output/exit-code/filter/JSON/seed/restart parity
- [ ] TUI workspace, forms, dialogs, keys, paging, async, and authorization parity
- [ ] GUI workspace, MVVM forms, actions, stale-work, shutdown, and auth parity
- [ ] Real cross-process writes are observable through every other surface
- [ ] Self-contained Windows, macOS, and Linux publish smoke tests
