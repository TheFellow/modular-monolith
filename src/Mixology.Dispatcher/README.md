# Domain event dispatcher

The dispatcher makes reciprocal bounded-context reactions explicit without a
runtime service locator. [`dispatcher.routes.json`](dispatcher.routes.json) is
the reviewed route manifest; `Mixology.DispatchGenerator` resolves its types
and emits committed C# in `Generated/DomainEventDispatcher.g.cs`.

## Dispatch phases

For each event, generated code resolves a fresh handler instance and executes:

1. every registered `PrepareAsync` step;
2. every mutating `HandleAsync` step;
3. one EF Core flush inside the shared command transaction;
4. every registered `FinalizeAsync` step.

Preparation can capture pre-mutation facts. Finalizers observe the complete
post-handler state, which keeps derived menu/order state independent of handler
order. Handlers receive `EventHandlerContext`; they may query/write the current
transaction and add audit touches but cannot publish another event.

Any handler or finalizer failure aborts dispatch and the unit-of-work middleware
rolls back the command, reactions, and successful audit entry together.

## Adding a reaction

Implement `IDomainEventHandler<TEvent>`, optionally the preparing/finalizing
interfaces, then add the fully qualified event and handler to the manifest.
Regenerate and commit the output:

```sh
dotnet run --project ../../tools/Mixology.DispatchGenerator -- \
  --manifest dispatcher.routes.json \
  --output Generated/DomainEventDispatcher.g.cs
```

Run the same command with `--check` to verify freshness. Tests should cover the
reaction through a real application session, rollback after a later failure,
fresh handler construction, and final state under route-order variation. Never
edit the generated file directly.
