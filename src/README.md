# Application and bounded contexts

`src` contains one modular application, not a collection of independently
deployed services. The .NET Generic Host is the outer composition mechanism;
modules own their domain contracts and storage mappings, while executables own
only process and presentation concerns.

## Foundation

- [`Mixology.Kernel`](Mixology.Kernel/README.md) contains stable value types,
  typed IDs, paging, and the cross-cutting error family.
- [`Mixology.Application`](Mixology.Application/README.md) owns sessions,
  operation pipelines, audit activity, metrics, and event dispatch ports.
- [`Mixology.Persistence`](Mixology.Persistence/README.md) owns SQLite/EF Core
  lifecycle and caller-owned units of work.
- [`Mixology.Filtering`](Mixology.Filtering/README.md) adapts Expr's checked filter
  AST, exact evaluator, and conservative LINQ pushdown planner.
- [`Mixology.Authorization.Cedar`](Mixology.Authorization.Cedar/README.md)
  adapts domain authorization contracts to `cedar-dotnet`.
- [`Mixology.Dispatcher`](Mixology.Dispatcher/README.md) is deterministic,
  generated event routing.
- [`Mixology.Presentation`](Mixology.Presentation/README.md) projects shared
  dashboard, navigation, actions, and tagged-mutation use cases without choosing
  a UI toolkit.

## Bounded contexts

| Module | Owns | May synchronously read |
| --- | --- | --- |
| Ingredients | catalog, substitutions, retirement | none |
| Drinks | recipes, lifecycle, review state | Ingredients |
| Inventory | stock, reservations, adjustments | Ingredients |
| Menus | curation, readiness, availability | Drinks, Ingredients, Inventory |
| Orders | accepted snapshots and lifecycle | Menus, Drinks, Ingredients, Inventory |
| Audit | immutable operation history | none |
| Tagging | polymorphic tag associations | registered domain target-loader ports |

Each `Mixology.Modules.*` project is a facade and an ownership boundary. Its
models, queries, requests, events, authorization vocabulary, EF model
configuration, and presentation projection live together. Private row types and
handlers must not become a back door between modules. The two `*.Contracts`
projects carry only the event/model contracts required to break reciprocal
Inventory/Orders dependencies.

Cross-context state changes are delivered as leaf event handlers. They may use
the caller's transaction but cannot publish another event. See
[architecture](../docs/architecture.md) for the enforceable dependency rules.

## Presentation surfaces

- [`Mixology.Cli`](Mixology.Cli/README.md) is the scriptable `System.CommandLine`
  surface.
- [`Mixology.Tui`](Mixology.Tui/README.md) is the Terminal.Gui workspace shell.
- [`Mixology.Gui`](Mixology.Gui/README.md) is the .NET MAUI MVVM client.
- [`Mixology.Seed`](Mixology.Seed/README.md) creates the canonical teaching data.

Surfaces share application behavior and presentation-neutral projections, not
view models. Each owns host lifetime, option parsing, error adaptation, input
mechanics, and rendering. The small
[`TUI`](Mixology.Toolkits.Tui/README.md) and
[`Desktop`](Mixology.Toolkits.Desktop/README.md) toolkits contain reusable UI
mechanics but cannot reference a domain module.
