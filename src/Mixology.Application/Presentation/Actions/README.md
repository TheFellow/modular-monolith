# Action presentation model

Action projection turns domain authorization and lifecycle rules into a small,
toolkit-neutral control state:

```csharp
new ActionState(id, Visible: true, Enabled: false, "publish blockers remain")
```

`ActionGroup` composes controls and child groups. Permissions inherit by
default, may be explicitly public, or may require an async authorization
delegate. Conditions are independent async lifecycle checks. Evaluation has two
different negative outcomes:

- a Cedar `Permission` denial makes the action invisible;
- a failed condition keeps it visible but disabled and supplies the first
  actionable reason.

Other authorization or condition failures remain typed errors; they are not
converted into hidden or disabled UI, because that would disguise an
application failure as a policy result. Duplicate/empty IDs and missing
declarations are `Internal` invariant errors.

## Adding an action

Define a stable `ActionId` in the owning module's projector, select the domain
authorization action/resource, add lifecycle conditions, and include it in the
appropriate group. Test allowed, denied, and disabled cases in the module, then
bind the resulting state independently in CLI help/behavior, TUI workspace, and
Desktop view model. Surfaces should never reproduce the Cedar rule.
