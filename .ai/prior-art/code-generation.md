# Dispatcher code generation

Status: Accepted  
Date: 2026-08-09

## Context

The reference generator emits readable event-to-handler type-switch wiring.
Handlers are constructed fresh, every optional preparation hook runs before any
mutation hook, handlers receive a restricted leaf context, and generated output
is committed and freshness-tested.

## Decision

Build a deterministic repository tool, `Mixology.DispatchGenerator`, that reads
explicit event/handler metadata and emits committed C#. Invoke it through a
documented `dotnet run` command and a CI freshness check. Generated code uses a
direct switch and typed DI resolution; it contains no runtime reflection.

An incremental source generator is not the primary mechanism. Roslyn generators
are excellent within one compilation, but the dispatcher must discover handler
metadata across module assemblies and the teaching repository benefits from a
reviewable committed artifact and an ordinary executable that can validate the
complete graph. Source generators remain appropriate for local boilerplate such
as MVVM properties.

## Required generated semantics

- A compile-time-known event switch and diagnostic for unknown required events.
- Fresh handler instances for each event dispatch.
- All `PrepareAsync` calls before any `HandleAsync` call.
- Restricted handler context with transaction, principal, and touch only.
- Stable output formatting but no semantic reliance on handler ordering.
- Generator failure for duplicate IDs, invalid signatures, or forbidden graphs.

## Sources

- [Roslyn source generator overview](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
- [`IIncrementalGenerator`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.iincrementalgenerator)
- [Reference dispatcher generator](https://github.com/TheFellow/go-modular-monolith/tree/main/pkg/dispatcher/gen)

