# Atomic tagged mutations

Status: Accepted

Date: 2026-08-09

## Inherited requirements

The Go `RunTaggedMutation` helper treats omitted tags differently from an
explicit empty set, validates a requested complete set before invoking the
domain mutation, and runs the mutation plus `Tags.Replace` in one transaction.
It participates in an existing transaction without taking commit or rollback
ownership. Both module operations retain their normal authorization, event,
touch, audit, and typed-error behavior.

## Decision

Keep the generic transaction mechanism in `Mixology.Application` and the
cross-module tagged composition in toolkit-neutral `Mixology.Presentation`.
`MixologySession.ExecuteAtomicAsync` supplies a transaction-bound session to a
mutation and continuation, flushing the first stage before the continuation so
newly created or updated targets can be queried in their post-mutation state.
It owns commit and rollback only when it opened the transaction; nested calls
participate in their caller's transaction.

`TaggedMutationCoordinator` composes a caller-provided owner-module mutation
with public `TaggingModule.ReplaceAsync`. Delegates select the entity UID and
copy the returned immutable `TagCollection` onto the owner model. This avoids a
common taggable interface on domain models and therefore avoids introducing a
module-to-Presentation or Application-to-Tagging dependency. A null collection
means the surface did not specify tags and bypasses the tag stage; the
non-null `TagCollection.Empty` means clear every tag. `TagCollection` validates
on construction, so invalid sets cannot reach the coordinator or execute the
mutation.

The coordinator preserves existing `AppError` instances and cancellation with
their exact identity. Unknown mutation, target-selection, or immutable-result
mapping failures become a safe `InternalError` retaining the original cause;
persistence save failures pass through the shared persistence translator. Each
owner/tag command still traverses its complete middleware pipeline, including
Cedar authorization, generated event dispatch, touch collection, and audit
recording. Intermediate saves remain inside the transaction and are removed by
rollback.

## Rejected alternatives

- Adding Tagging to `Mixology.Application` would reverse the intended assembly
  direction and make the generic application foundation domain-aware.
- Having each TUI or GUI view model coordinate transactions would duplicate a
  security- and consistency-sensitive workflow across toolkits.
- Updating tags directly through EF would bypass Tagging authorization,
  post-state ABAC checks, audit semantics, and target registration.
- A mutable `ITaggableEntity` contract would fit the Go shape poorly: the C#
  owner models are immutable records and should not depend on a higher-layer
  presentation contract.

## Validation

Real SQLite migrations, the generated dispatcher, the audit writer, and Cedar
authorization prove joint commit; post-state tag denial rolls back domain
state, tag state, event-derived touches, and audit rows; domain failure skips
tag selection; omitted tags preserve while explicit empty tags clear; nested
composition leaves transaction ownership with the caller; and typed errors and
cancellation are rethrown unchanged. Raw delegate failures are safely typed,
retain their cause, and roll back both stages.

## Sources

- Reference composition: `/go-modular-monolith/app/tagged_mutation.go`
- Reference tests: `/go-modular-monolith/app/tagged_mutation_test.go`
- Reference CLI optional-tag parsing: `/go-modular-monolith/main/cli/helpers.go`
- [.NET transactions in EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [.NET cancellation model](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads)
