# Backing storage

Status: Accepted  
Date: 2026-08-09

## Context

The Go reference builds around bstore, one embedded file, explicit read/write
transactions, domain-owned persistence models, and provider pushdowns followed
by exact residual filtering. Domain writes, generated event handlers, entity
touches, and successful audit records share one atomic transaction. The .NET
store must preserve those semantics and publish with each desktop executable.

## Candidates

| Candidate | Strength | Material drawback | Result |
| --- | --- | --- | --- |
| EF Core 10 + SQLite | LINQ, migrations, constraints, transactions, JSON aggregates | More machinery and native RID assets | Choose |
| LINQ to DB + SQLite | Thin, capable typed SQL/LINQ | No integrated model-diff migration workflow | Runner-up |
| sqlite-net-pcl | Small and desktop friendly | Limited relationships, LINQ, and migration history | Reject |
| LiteDB | Pure C# embedded documents | Non-relational query model and weak migrations | Reject |
| Dapper + SQLite | Explicit and small | Requires custom query compiler and migration system | Reject |
| PostgreSQL | Excellent relational behavior | Not embedded or self-contained | Reject |

## Decision

Use `Microsoft.EntityFrameworkCore.Sqlite` 10.0.10 with an explicit
`SQLitePCLRaw.bundle_e_sqlite3` 3.0.5 pin. EF Core and Microsoft.Data.Sqlite are
MIT licensed, SQLitePCLRaw is Apache-2.0, and SQLite is public domain.

One application `DbContext` model and one context per operation make the
cross-domain transaction explicit. Modules retain internal row types and
`IEntityTypeConfiguration<T>` mappings; composition applies every module's
mapping. Commands begin an explicit transaction carried by the operation
context. Reads reuse it when present or use a short-lived no-tracking context.
No `DbContext` is used concurrently.

Primary entities, tags, reservations, and audit entries use relational tables.
Aggregate-owned recipe, menu, order, usage, and touch values may use EF Core 10
JSON complex types when they do not need independent identity. Money persists
as integer minor units and timestamps as UTC `DateTime` because SQLite cannot
faithfully order every .NET `decimal` or `DateTimeOffset` value.

Migrations are checked in through a separate migrations project. Startup may
apply them only after a safe SQLite backup and must fail closed. The CLI also
gets an explicit migration command. Enable foreign keys, WAL, and finite busy
timeouts; keep the database device-local and write transactions short.

## Filtering contract

The application-owned AST produces both an exact evaluator over a hydrated
view and a conservative `Expression<Func<TRow, bool>>` for `IQueryable.Where`.
Only logically implied predicates are pushed down. Exact evaluation still runs
after derived fields and tags are hydrated, before authorization and page
completion. Provider translation is never the semantic authority.

## Known limitations

- SQLite admits one writer at a time.
- Microsoft.Data.Sqlite async APIs execute synchronously, so UI dispatchers must
  never perform store work directly.
- Some schema changes rebuild tables; interrupted migrations require recovery.
- WAL databases have companion files and must use SQLite backup/checkpoint rules.
- Native RID assets require publish-and-run tests on each supported OS.
- EF NativeAOT dynamic-query support is experimental and unsuitable here.

## Validation gates

- Create an empty database and upgrade from every checked-in migration.
- Roll back a domain write, all leaf handlers, touches, and audit atomically.
- Compare exact evaluator results with pushed-query results over the Go corpus.
- Cover OR, negation, tag hydration, authorized page filling, and query plans.
- Restart every surface against one file and smoke-test self-contained publishes.

## Sources

- [EF Core repository and license](https://github.com/dotnet/efcore)
- [EF Core 10 features and LTS](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)
- [EF Core SQLite provider](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)
- [SQLite provider limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)
- [EF transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [EF migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Microsoft.Data.Sqlite transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions)
- [Microsoft.Data.Sqlite asynchronous limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async)
- [SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw)
- [SQLite appropriate uses](https://www.sqlite.org/whentouse.html)
- [SQLite WAL](https://www.sqlite.org/wal.html)
- [EF NativeAOT query limitations](https://learn.microsoft.com/en-us/ef/core/performance/nativeaot-and-precompiled-queries)

