# Semantic port roadmap

The implementation order follows the intent of the
[Building Mixology series](https://thefellow.github.io/series/mixology/) while
moving the foundation and CLI ahead of persistent interfaces.

1. Pin the SDK, analyzers, package policy, decision records, and parity ledger.
2. Port kernel types: IDs, errors, paging, currency, money, measurements, tags,
   and quality.
3. Establish SQLite storage, migrations, transaction ownership, and a
   production-shaped test fixture.
4. Integrate a typed filter grammar, checked owned AST, canonical printer, exact
   evaluator, and conservative LINQ pushdown planner.
5. Build the Generic Host composition shape, authentication, operation context,
   middleware, telemetry, audit protocol, and generated leaf event dispatch.
6. Deliver Ingredients plus the real CLI and seeder as the first vertical slice.
7. Add Drinks, Inventory, and Menus, including atomic events and readiness.
8. Add Orders and prove the reciprocal reservation and availability workflow.
9. Add retirement, replacement, degradation, and historical-snapshot behavior.
10. Integrate `cedar-dotnet`, Tagging, ABAC, authorization-filtered pages, and
    action projection. Complete CLI parity and restart tests.
11. Add the Terminal.Gui TUI with deterministic input and rendering tests.
12. Add the Avalonia MVVM desktop client with headless control and visual tests.
13. Prove cross-surface durability, architecture rules, self-contained publishes,
    migrations, and generated-output freshness on every supported OS.

Each numbered slice should land as one or more independently building, tested
commits. The behavioral source is `/go-modular-monolith`; tutorial prose guides
the reveal but never overrides observable behavior.
