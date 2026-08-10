# Typed filter expressions

`Mixology.Filtering` adapts the general-purpose
[`Expr`](https://www.nuget.org/packages/Expr) package for list operations. A
module declares a public filter view and schema; Expr supplies parsing, static
checking, the public AST, canonical display, optimization, and exact in-memory
evaluation. The adapter adds typed application errors and safe EF Core
narrowing.

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
        Filter.Field<DrinkFilterView, string[]>(
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
parses and type-checks with Expr before database work starts. Unknown fields,
incompatible values, invalid constant dates, durations, and patterns become
typed `Invalid` errors. A `FilterExpression<T>` retains its trimmed source,
canonical string, public Expr `SyntaxNode` tree, and exact `Match` behavior.

The full checked Expr language is available, including arithmetic, comparisons,
boolean logic, collections, and predicates. Existing Mixology filters remain
compatible: method spellings such as `name.contains("gin")`, collection
membership such as `tags contains "featured"`, and dotted schema fields are
rewritten to equivalent Expr syntax before checking.

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
