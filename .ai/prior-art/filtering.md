# Typed filtering

Status: Accepted  
Date: 2026-08-09

## Decision

Port the reference filter language as repository-owned code: lexer, Pratt
parser, type checker, immutable record AST, canonical printer, exact expression
evaluator, and conservative EF/LINQ planner. Do not expose EF expressions,
Dynamic LINQ, SQL, or a third-party parser as the public filter contract.

The grammar retains comparisons, `in`/`not in`, `&&`/`and`, `||`/`or`,
`!`/`not`, string and collection `contains`, `startsWith`, `endsWith`,
`matches`, and checked literal-only `date()` and `duration()`. Arithmetic,
arbitrary calls, unknown fields, incompatible types, and invalid regexes fail
before a query executes. Expressions preserve source text, canonical text, and
an application-owned tree.

The LINQ planner ports bstore's implication algorithm. It pushes comparisons,
booleans, `in`, negation, conjunctions, and constraints common to every OR arm
only when a schema supplies a strongly typed persisted-property selector. The
complete evaluator always runs after tags and derived fields are hydrated.

## Rejected shortcuts

- `System.Linq.Dynamic.Core` couples public syntax and safety to a runtime parser
  and does not give the application the required stable AST.
- A direct AST-to-SQL compiler bypasses EF's typed provider and duplicates
  parameterization and conversion behavior.
- Executing only the EF predicate changes semantics around hydration, Unicode,
  collation, nulls, and regex dialects.

## Validation

Port the reference parser, canonicalization, evaluator, and pushdown tests.
Property tests compare exact evaluation with candidate-plus-residual evaluation
for every AST shape. Provider integration tests inspect both results and query
plans without asserting unstable generated SQL text.

## Sources

- [LINQ expression trees](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/)
- [EF Core query evaluation](https://learn.microsoft.com/en-us/ef/core/querying/client-eval)
- [SQLite provider function mappings](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/functions)
- [`go-modular-monolith/pkg/filter`](https://github.com/TheFellow/go-modular-monolith/tree/main/pkg/filter)

