# Canonical seed process

`Mixology.Seed` creates the teaching dataset through the same authorized,
audited module APIs used by the three interactive surfaces. It creates 18
ingredients with inventory, six classic drinks, and one published menu.

```sh
MIXOLOGY_DB=../../data/mixology.db dotnet run --project .
```

The default is `data/mixology.db`; `MIXOLOGY_DB` supplies the environment
default. Seeding is intentionally non-idempotent: an existing canonical entity
causes a typed conflict instead of guessing whether partially matching data may
be overwritten. Each public command is atomic, while commands successfully
committed before a later seed failure remain durable.

Keeping Seed as a composition root rather than an SQL importer proves that
validation, Cedar authorization, middleware, events, audit, and migrations work
together from an empty database.
