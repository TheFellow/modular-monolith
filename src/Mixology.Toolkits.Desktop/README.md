# Desktop toolkit

`Mixology.Toolkits.Desktop` holds small MVVM concurrency seams that are reusable
without referencing .NET MAUI controls or a Mixology module. The executable owns
shells, dialogs, navigation, and visual styling.

`IUiDispatcher` is the boundary for publishing observable state. Production
uses the .NET MAUI UI thread; tests use `ImmediateUiDispatcher` or a recording
implementation without weakening the view model's threading contract.

`LatestRequest<T>` gives every accepted asynchronous request a generation,
cancels previous generations, and returns `IsCurrent = false` to a superseded
completion. Disposal prevents new requests, cancels all accepted requests, and
observes/drains their tasks so background faults do not escape process shutdown.

Typical view-model refresh:

```csharp
LatestResult<DrinkPage> result = await latest.RunAsync(
    token => drinks.ListAsync(request, token), cancellationToken);
if (result.IsCurrent)
{
    await dispatcher.InvokeAsync(() => Items = result.Value!.Items, cancellationToken);
}
```

Keep confirmation dialogs and application error presentation in Desktop: they
require application/window ownership and domain language, neither of which is a
generic concurrency concern.
