# Command and query middleware

The operation pipeline centralizes cross-cutting behavior around public module
operations. A module supplies the final delegate; it does not manually open a
transaction, emit a timing metric, or repeat surface-specific error handling.

## Default pipelines

Queries run:

```text
serialization -> logging -> metrics -> module query
```

Commands run:

```text
serialization -> logging -> metrics -> activity tracking -> unit of work
              -> successful activity recording -> event dispatch -> module command
```

The delegate nesting means entry is left-to-right and completion unwinds in the
opposite direction. The unit-of-work boundary therefore contains the command,
successful audit record, and all generated event reactions. On failure it rolls
back and the activity recorder persists the rejected attempt separately.

## Operation context

`OperationContext` carries the actor, cancellation, current `StoreSession`,
activity, event queue, and event-handler restriction. `OperationChain` calls
`ForOperation` before every invocation so mutable per-operation state is never
reused between commands.

Domain code may:

- require the transaction-bearing session for a command;
- add an event after the owning mutation is staged;
- touch direct or indirectly affected Cedar entities for audit attribution;
- use the same cancellation token for database and authorization work.

Event handlers receive a restricted `EventHandlerContext`. They can participate
in the existing transaction and record touches but cannot enqueue cascading
events. This makes the dispatcher graph finite and visible in generated source.

## Extending the pipeline

Add cross-cutting behavior only when every operation of a kind needs it. Make a
middleware class with `InvokeAsync`, register one lifetime-safe instance, place
it explicitly in `OperationPipeline`, and test ordering, success, failure,
cancellation, and fresh-context behavior. Middleware should preserve an
existing `AppError`; unexpected failures are wrapped as `Internal` at the
boundary that can add meaningful operation context.

Authorization normally remains in module requests because it needs the loaded
resource and, for mutations, often both pre- and post-state. Lists must hydrate,
evaluate the exact filter, authorize each candidate, skip only permission
denials, and continue until the visible page is full.
