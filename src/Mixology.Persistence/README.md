# Persistence store

`Mixology.Persistence` is the application boundary around EF Core and SQLite.
It owns database lifecycle, migration startup, sessions, transaction ownership,
error translation, and model-composition seams. Each module continues to own
its private rows, mappings, and queries; there is no repository-shaped global
domain model.

## Lifecycle and model registration

Composition calls `AddMixologyPersistence(databasePath, migrationsAssembly)`
and registers every module's `IModuleModelConfiguration`. `MixologyStore.InitializeAsync`
creates the parent directory, applies checked-in migrations, enables WAL and
foreign keys, and creates the one metadata row for a new database.

`MixologyDbContext.OnModelCreating` delegates to registered module mappings.
Keep EF indexes, conversions, owned shapes, and row-to-domain conversion inside
the owning module. Add a migration after a model change and run the pending-model
check described in [development](../../docs/development.md#full-validation).

## Sessions and transaction ownership

`MixologyStore.OpenSessionAsync` creates a disposable `StoreSession`. Query
operations may use a short-lived context. The command pipeline begins one write
transaction, makes its `StoreSession` available through `OperationContext`, and
owns commit or rollback.

Every mutation, generated event reaction, finalizer, and successful audit entry
uses that same session. A repository must never create a second transaction to
escape the caller's unit of work. `MixologySession.ExecuteAtomicAsync` is the
explicit composition seam for two application stages that must share a write:
it flushes between stages so the continuation can query the mutation, then only
the outer owner commits.

## Errors and filtering

`PersistenceErrors` maps database failures at the persistence boundary:
constraint collisions become `Conflict`, invalid stored/input shapes become
`Invalid` where appropriate, and unexpected provider failures become `Internal`
with the original exception retained. Entity absence is detected by the owning
repository and reported as `NotFound` with domain context.

Repositories may apply the safe candidate expression described in the
[filter guide](../Mixology.Filtering/README.md), but must hydrate and evaluate
the residual expression before authorization and paging.

## Tests

Persistence tests use a unique SQLite path, real migrations, and disposal. Do
not replace the provider with EF's in-memory implementation: it would skip SQL
translation, constraints, transactions, and WAL behavior that this boundary is
responsible for teaching.
