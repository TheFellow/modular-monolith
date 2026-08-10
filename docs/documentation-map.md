# Documentation parity map

The Go reference and .NET port organize presentation code differently. This map
makes semantic documentation parity auditable without manufacturing one C#
project for every Go package. “Consolidated” means the destination explicitly
covers the source topic inside the .NET ownership boundary that implements it.

## Repository and application guides

| Go reference document | .NET teaching document | Adaptation |
| --- | --- | --- |
| `README.md` | [`README.md`](../README.md) | repository start and surface overview |
| `app/kernel/readme.md` | [`Mixology.Kernel/README.md`](../src/Mixology.Kernel/README.md) | foundational value types |
| `app/domains/readme.md` | [`src/README.md`](../src/README.md) | bounded contexts and surface boundaries |
| `main/cli/README.md` | [`Mixology.Cli/README.md`](../src/Mixology.Cli/README.md) | System.CommandLine entrypoint |
| `main/tui/README.md` | [`Mixology.Tui/README.md`](../src/Mixology.Tui/README.md) | Terminal.Gui composition and workspaces |
| `main/gui/README.md` | [`Mixology.Desktop/README.md`](../src/Mixology.Desktop/README.md) | .NET MAUI MVVM composition and lifecycle |

## Foundation and application mechanics

| Go reference document | .NET teaching document | Adaptation |
| --- | --- | --- |
| `pkg/authz/README.md` | [`Mixology.Authorization.Cedar/README.md`](../src/Mixology.Authorization.Cedar/README.md) | cedar-dotnet adapter and module contracts |
| `pkg/dispatcher/README.md` | [`Mixology.Dispatcher/README.md`](../src/Mixology.Dispatcher/README.md) | manifest-driven C# generation and phases |
| `pkg/errors/README.md` | [`Kernel/Errors/README.md`](../src/Mixology.Kernel/Errors/README.md) | strongly typed cross-cutting errors |
| `pkg/filter/README.md` | [`Mixology.Filtering/README.md`](../src/Mixology.Filtering/README.md) | checked AST and LINQ/EF pushdowns |
| `pkg/middleware/README.md` | [`Application/Operations/README.md`](../src/Mixology.Application/Operations/README.md) | explicit .NET middleware chains |
| `pkg/presentation/actions/README.md` | [`Application/Presentation/Actions/README.md`](../src/Mixology.Application/Presentation/Actions/README.md) | toolkit-neutral action projection |
| `pkg/store/README.md` | [`Mixology.Persistence/README.md`](../src/Mixology.Persistence/README.md) | EF Core, SQLite, sessions, migrations |
| `pkg/telemetry/README.md` | [`Mixology.Application/README.md`](../src/Mixology.Application/README.md#telemetry) | standard logging, metrics, and exporters; consolidated with its owning instruments |
| `pkg/testutil/README.md` | [`tests/README.md`](../tests/README.md) | project-local production-shaped fixtures and UI seams |

## Presentation toolkits

| Go reference document | .NET teaching document | Adaptation |
| --- | --- | --- |
| `pkg/toolkits/readme.md` | [`src/README.md`](../src/README.md#presentation-surfaces) | toolkit ownership and dependency direction |
| `pkg/toolkits/cli/readme.md` | [`Mixology.Cli/README.md`](../src/Mixology.Cli/README.md#cli-entrypoint-and-toolkit) | consolidated: System.CommandLine and serializers are used directly |
| `pkg/toolkits/cli/table/readme.md` | [`Mixology.Cli/README.md`](../src/Mixology.Cli/README.md#run-and-discover) | consolidated: stable command-local text tables replace reflection rendering |
| `pkg/toolkits/gui/readme.md` | [`Mixology.Toolkits.Desktop/README.md`](../src/Mixology.Toolkits.Desktop/README.md) and [`Mixology.Desktop/README.md`](../src/Mixology.Desktop/README.md) | reusable concurrency separated from .NET MAUI-owned shell/dialogs |
| `pkg/toolkits/tui/readme.md` | [`Mixology.Toolkits.Tui/README.md`](../src/Mixology.Toolkits.Tui/README.md) | one cohesive Terminal.Gui mechanics project |
| `pkg/toolkits/tui/components/readme.md` | [`TUI toolkit: Host and component lifecycle`](../src/Mixology.Toolkits.Tui/README.md#host-and-component-lifecycle) | consolidated |
| `pkg/toolkits/tui/dialog/readme.md` | [`TUI toolkit: Forms and dialogs`](../src/Mixology.Toolkits.Tui/README.md#forms-and-dialogs) | consolidated |
| `pkg/toolkits/tui/forms/readme.md` | [`TUI toolkit: Forms and dialogs`](../src/Mixology.Toolkits.Tui/README.md#forms-and-dialogs) | consolidated |
| `pkg/toolkits/tui/keyname/readme.md` | [`TUI toolkit: Keys, key names, and routing`](../src/Mixology.Toolkits.Tui/README.md#keys-key-names-and-routing) | Terminal.Gui `Key` is canonical |
| `pkg/toolkits/tui/keys/readme.md` | [`TUI toolkit: Keys, key names, and routing`](../src/Mixology.Toolkits.Tui/README.md#keys-key-names-and-routing) | consolidated |
| `pkg/toolkits/tui/styles/readme.md` | [`TUI toolkit: Styles and errors`](../src/Mixology.Toolkits.Tui/README.md#styles-and-errors) | semantic error styles; executable owns theme |

## Long-form docs

| Go reference document | .NET teaching document |
| --- | --- |
| `docs/architecture.md` | [`architecture.md`](architecture.md) |
| `docs/development.md` | [`development.md`](development.md) |
| `docs/features.md` | [`features.md`](features.md) |

The .NET repository also keeps the port-specific
[`roadmap`](roadmap.md), [`semantic parity ledger`](semantic-parity.md), and
open-source research under [`.ai/prior-art`](../.ai/prior-art/README.md).
