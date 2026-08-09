# Semantic parity ledger

This ledger tracks observable parity with `/go-modular-monolith`. A checked item
requires production-shaped tests, not only a matching type or method name.

## Foundation

- [x] Prefixed, strongly typed IDs for drinks, ingredients, inventory, menus,
  orders, and audit entries
- [x] Typed application errors with safe messages and CLI/HTTP/gRPC mappings
- [x] Cursor paging primitives, complete traversal, and permission-aware page filling
- [x] Money, currency, volume/discrete measurement, tags, and quality values
- [x] Checked filter grammar, canonical AST, exact evaluator, and safe EF pushdowns
- [x] Shared SQLite store, initial migration, explicit session transaction, and rollback
- [x] Fresh operation contexts, middleware order, telemetry, and atomic auditing
- [x] Cedar policies/entities through the pinned `cedar-dotnet` source adapter
- [x] Generated two-phase, non-cascading event dispatcher infrastructure

## Domains

- [x] Ingredients CRUD, filters, retirement, and permanent-replacement intent
- [x] Drinks CRUD, recipes, typed filters, authorization, and lifecycle
- [x] Drinks retirement reactions, review state, and substitution rewrites
- [x] Inventory on-hand/reserved/available stock and adjustment reasons
- [x] Menus curation, publication, availability, analytics, and readiness
- [x] Orders placement, lifecycle, typed filtering, authorization, and blocked-state model
- [x] Append-only audit history, actor queries, and touched resources
- [x] Polymorphic tags, registered target loaders, atomic tagged mutations, ABAC

## Stateful workflows

- [x] Placement captures an immutable ingredient-usage snapshot with substitution planning
- [x] Placement reserves that snapshot atomically
- [x] Completion consumes reservations; cancellation releases them
- [x] Shortage blocks every affected pending order; restock unblocks it
- [x] Retirement rewrites only compatible explicit replacements
- [x] Required retired references cause review; optional references disappear
- [x] Accepted order ingredient names and amounts remain historical truth
- [x] Published menus degrade honestly; drafts with blockers cannot publish
- [ ] Event handler order cannot change outcomes and any failure rolls back all

## Surfaces

- [x] CLI Ingredients, Drinks, Inventory, and Audit command/output/JSON/restart behavior
- [x] CLI Menus parity
- [x] CLI Orders parity
- [x] CLI Tags parity
- [ ] CLI seed, logging, and metrics parity
- [ ] TUI workspace, forms, dialogs, keys, paging, async, and authorization parity
- [ ] GUI workspace, MVVM forms, actions, stale-work, shutdown, and auth parity
- [ ] Real cross-process writes are observable through every other surface
- [ ] Self-contained Windows, macOS, and Linux publish smoke tests
