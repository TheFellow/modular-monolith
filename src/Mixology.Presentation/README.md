# Presentation model

`Mixology.Presentation` is shared application-facing presentation, not a UI
framework. It combines public module APIs into dashboard, navigation, and
tagged-mutation use cases. It references no System.CommandLine, Terminal.Gui,
or Avalonia type, so each executable can choose native interaction patterns.

`DashboardService` assembles aggregates and recent activity. `NavigationProjector`
uses domain action projections to include only workspaces visible to the actor.
Unexpected projection failures are returned alongside a conservative navigation
item rather than silently looking like a Cedar denial.

`TaggedMutationCoordinator` executes a domain mutation and optional tag
replacement inside one `MixologySession.ExecuteAtomicAsync` transaction. The
post-mutation stage reloads/authorizes the target, so pre- and post-state ABAC
both remain effective.

See [action projection](../Mixology.Application/Presentation/Actions/README.md)
for the capability model consumed by navigation and surface controls.
