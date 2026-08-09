# Architecture

Mixology is one deployable application. Seven bounded contexts own their public
contracts, commands, persistence mappings, Cedar policies, events, and surface
adapters. The .NET Generic Host owns configuration, dependency injection,
logging, lifetime, and the explicit composition root.

## Context map

| Context | Owns | Synchronous dependencies |
| --- | --- | --- |
| Ingredients | catalog and retirement | none |
| Drinks | recipes and review state | Ingredients |
| Inventory | stock and reservations | Ingredients |
| Menus | curation, readiness, publication | Drinks, Ingredients, Inventory |
| Orders | accepted snapshots and lifecycle | Menus, Drinks, Ingredients, Inventory |
| Audit | append-only activity | none |
| Tagging | polymorphic authorized tags | registered target-loader ports |

Reciprocal workflows communicate through public queries and generated event
routing. Event handlers are leaf operations: their restricted context can use
the current transaction and touch audit resources, but cannot publish another
event. Every handler is constructed fresh; all preparation hooks run before any
mutation hook so correctness does not depend on handler order.

## Operation boundary

Commands pass through serialization, logging, metrics, activity tracking, a
unit of work, authorization of loaded and resulting state, execution, event
dispatch, and successful audit recording. The mutation, leaf handlers, touches,
and successful audit entry commit atomically. A failure rolls that transaction
back and records the failed attempt separately.

Gets authorize their result. Lists hydrate and filter before authorizing each
result, omit only permission denials, and keep reading until the visible page is
full. Counts and cursors describe only visible results.

## Dependency direction

- Kernel and shared infrastructure reference no domain or presentation project.
- A module consumes only another module's public models, queries, or events.
- Presentation composes public module capabilities and read models without a UI
  toolkit; executables and future surfaces depend on it, never the reverse.
- Toolkits reference neither application modules nor sibling toolkits.
- A domain surface references its matching toolkit and public application API.
- Executables compose modules and surfaces but contain no business rules.
- Architecture tests and project references make these rules executable.
