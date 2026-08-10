# Cedar authorization

`Mixology.Authorization.Cedar` adapts module-owned authorization vocabulary to
the pinned [`cedar-dotnet`](../../external/cedar-dotnet/README.md) source tree.
It keeps Cedar schema, policy, entity construction, and evaluation out of
presentation and persistence code.

## Composition

Each bounded context implements `ICedarAuthorizationModule` and contributes:

- a named Cedar schema;
- the resource types validated by that schema;
- one or more named policy documents;
- domain mapping code that constructs a complete Cedar resource entity.

`CedarAuthorizer` builds one deterministic catalog. It rejects duplicate
resource types or policy document names and validates every parsed policy at
startup. A malformed catalog is a programming/configuration failure and becomes
a typed `Internal` error before the surface begins serving work.

## Evaluation

Domain authorization contracts expose action identifiers and construct a
resource from the loaded domain model. `IEntityAuthorizer.AuthorizeAsync` then:

1. selects the validator for the resource type;
2. validates the resource and request;
3. evaluates the shared policy set using the actor and resource entities;
4. returns on `Allow`, throws `Permission` on a denial, or throws `Internal` for
   invalid input/evaluator diagnostics.

Cancellation is preserved rather than reclassified. A list catches only
`Permission` to omit a row; evaluator failures must stop the operation so an
outage cannot masquerade as an empty list.

## Changing a policy

Change the domain action/resource mapping, schema, and policy together. Cover
owner, role personas, anonymous access, explicit denial, malformed resources,
and list filtering in the module's authorization tests. Action projection and
navigation call the same domain contract, so a policy change is automatically
reflected in CLI behavior and the visible TUI/Desktop capabilities.
