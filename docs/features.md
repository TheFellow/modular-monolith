# Application features

Examples assume the [seed process](../src/Mixology.Seed/README.md) has created
`data/mixology.db`. Replace `mixology` with
`dotnet run --project src/Mixology.Cli --` when running from source.

## Filtering and paging

Every list uses the shared [typed filter language](../src/Mixology.Filtering/README.md).
Use `--filter-help` to see the fields and examples for one domain without
guessing its persistence shape.

```sh
mixology drinks list --filter-help
mixology drinks list --filter 'name.contains("rum") && status == "active"'
mixology inventory list --filter 'quantity <= 5 && unit in ["ml", "oz"]'
mixology audit list --limit 20 --cursor aud-...
```

Parsing produces an application-owned, type-checked tree. A conservative LINQ
expression narrows the EF Core query; the complete expression is evaluated over
the hydrated filter view so an optimization cannot alter results. Pages are
authorized per row and continue reading candidates until the visible page is
full.

## Tags

Drinks, ingredients, inventory, menus, and orders accept label tags
(`featured`) and key/value tags (`region=west`). Audit entries are deliberately
not taggable. Keys are case-sensitive and unique per entity; adding an existing
key replaces it, and removing a missing key is a successful no-op.

```sh
mixology tags add drk-abc123 featured
mixology tags add drk-abc123 audience=sommelier
mixology tags list drk-abc123
mixology tags remove drk-abc123 audience
mixology tags summary
mixology drinks list --filter 'tags contains "featured"'
```

Domain changes and tag replacement can share one caller-owned transaction
through `TaggedMutationCoordinator`. Each domain remains responsible for
loading its target and exposing the Cedar entity used to authorize the tag
operation.

## Authorization personas

All interactive surfaces accept `--actor` (also `--as` in the CLI). `owner` has
full access; `manager`, `sommelier`, and `bartender` see role-appropriate
workflows; `anonymous` is public read-only. Cedar authorizes operations,
resources, individual list rows, action buttons, and navigation destinations.

```sh
mixology --actor bartender menus list
mixology --as anonymous drinks list
```

See [the Cedar adapter guide](../src/Mixology.Authorization.Cedar/README.md) for
the distinction between a denied request and an evaluator failure.

## IDs and structured input

Strongly typed identifiers carry their entity prefix: `drk-`, `ing-`, `inv-`,
`mnu-`, `ord-`, and `aud-`. Primary IDs use `--id`; references name their
target, such as `--menu-id` or `--drink-id`. Cross-domain tag commands infer the
resource type from operational prefixes and reject audit IDs.

List, get, and mutation commands support JSON output where the reference
surface does. Document-shaped mutations accept file/stdin input and expose
templates from their command help. Invalid identifiers and JSON are classified
as typed `Invalid` errors rather than leaking parser exceptions.

## Audit

Every command pipeline creates an operation activity. Direct and event-driven
mutations add touched resources. A successful audit entry commits with the
write; a rejected attempt is recorded independently after rollback.

```sh
mixology audit list --limit 20
mixology audit list --principal owner
mixology audit list --entity Mixology::Drink::drk-abc123
mixology audit history Mixology::Drink::drk-abc123
```

## Fulfillment and retirement

Order placement captures an immutable ingredient-usage snapshot and reserves
stock. Completion consumes reservations; cancellation releases them. A stock
correction below the reserved total blocks every affected pending order, while
replenishment restores it to pending. Blocked orders remain cancellable but
cannot be completed.

Ingredient retirement removes current stock while preserving historical order
truth. Required canonical recipe references move a drink to review-required;
optional references disappear. An explicit replacement must have compatible
category and measurement dimension before recipes are rewritten. The system
never promotes a temporary substitution rule to a permanent replacement.

Published menus remain published when their environment degrades. Availability
and readiness explain the blockers. Draft publication rejects missing or
retired requirements, review-required drinks, unavailable items, and temporary
substitutions; ordinary low stock is a warning. Existing orders retain the
snapshot accepted at placement even after recipes change.

These reciprocal reactions run through the generated dispatcher inside the
originating transaction. Handler order cannot change the derived state, and a
failure rolls back the complete collaboration.

## Runtime configuration

CLI, TUI, Desktop, and Seed accept `--db`; interactive surfaces also accept
`--actor`, `--log-level`, `--log-format`, `--log-file`, and `--metrics` where
appropriate. Corresponding `MIXOLOGY_*` environment values supply defaults,
and explicit command-line values win. Prometheus export listens on
`localhost:9090/metrics` only when enabled.

Surface-specific lifecycle and keyboard behavior is documented in the
[CLI](../src/Mixology.Cli/README.md), [TUI](../src/Mixology.Tui/README.md), and
[Desktop](../src/Mixology.Desktop/README.md) guides.
