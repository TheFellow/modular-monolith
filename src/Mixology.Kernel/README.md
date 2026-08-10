# Kernel

`Mixology.Kernel` contains the small, stable vocabulary shared by every bounded
context. It has no dependency on persistence, hosting, authorization, or a
presentation surface.

## Contents

- `Entities` provides prefixed `readonly record struct` identifiers and the
  shared `EntityUid` used by Cedar and audit records.
- `Errors` provides the strongly typed application error family documented in
  [the error guide](Errors/README.md).
- `Paging` provides opaque cursors and result pages.
- `Measurement`, `Money`, and `Quality` encode validated domain values rather
  than passing primitive strings and decimals through the application.
- `Tags` parses canonical label and key/value tags and preserves key uniqueness.

Kernel values validate at construction. They are immutable, compare by value,
and keep their transport spelling close to the type that owns it. A module
should add a type here only when the concept is genuinely shared and stable; a
domain-specific enum or entity belongs in its module.

Closed variants use explicit record hierarchies and exhaustive pattern matching
because the pinned stable C# toolchain does not yet supply native discriminated
unions. Do not simulate openness with arbitrary strings when the set is part of
the domain contract.
