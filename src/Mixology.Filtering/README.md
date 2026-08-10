# Typed filter expressions

`Mixology.Filtering` is the transport-neutral expression language used by list
operations. A module declares a public filter view and schema; parsing produces
a checked, application-owned AST that supports canonical display, exact
in-memory evaluation, and safe EF Core narrowing.

## Data path

```text
request text -> Parse(schema) -> checked FilterExpression<T>
                              -> conservative LINQ pushdown over private rows
                              -> hydrate complete public filter view
                              -> exact Matches(view)
                              -> authorize rows and fill page
```

The schema is an application contract, not a database model. Derived fields and
hydrated tags can therefore remain filterable without exposing private row
types.

## Declaring a schema

Use typed expressions rather than reflection metadata:

```csharp
FilterSchema<DrinkFilterView> schema = new(
    [
        Filter.Field<DrinkFilterView, string>("id", value => value.Id, "Drink ID"),
        Filter.Field<DrinkFilterView, string>("name", value => value.Name, "Drink name"),
        Filter.Field<DrinkFilterView, IReadOnlyList<string>>(
            "tags", value => value.Tags, "Tags (key or key=value)"),
    ],
    "name.contains(\"gin\")",
    "tags contains \"featured\"");
```

Field names are stable user-facing API. Descriptions and examples feed CLI and
interactive help. A separate persisted-field map identifies the module-owned
row selectors for which pushdown is semantically safe.

## Parsing and evaluation

`Filter.Parse(schema, source)` returns `null` for empty input; otherwise it
lexes, parses, and type-checks before database work starts. Unknown fields,
incompatible values, unsupported constructs, and invalid patterns become typed
`Invalid` errors. A `FilterExpression<T>` retains its trimmed source, canonical
string, owned `FilterNode` tree, and exact `Match` behavior.

Supported forms include comparisons (`==`, `!=`, `<`, `<=`, `>`, `>=`, `in`,
`not in`), boolean logic, parentheses, negation, and string/collection
predicates (`contains`, `startsWith`, `endsWith`, and `matches`). The language
intentionally rejects arithmetic and arbitrary method calls.

## LINQ/EF Core pushdown

`BuildPushdown` extracts only constraints logically required by the complete
tree and returns a LINQ expression over the persistence row. Repositories apply
that expression to `IQueryable`, batch-hydrate derived data, and still evaluate
the entire expression. A pushdown produces candidates, never final matches.

This rule is the central correctness property: changing the planner may improve
query cost but cannot change the result set. Keep hydrated fields such as tags
out of the persistence map. When extending a filter, update the schema, complete
view projection, help examples, parse/match tests, and a SQLite equivalence test
for every new pushdown.
