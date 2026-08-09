# Entity identifiers

Status: Accepted  
Date: 2026-08-09

## Decision

Preserve the reference's prefixed KSUID text contract: `drk`, `ing`, `inv`,
`mnu`, `ord`, and `aud`, followed by `-` and the canonical 27-character KSUID.
Use MIT-licensed `KsuidDotNet` 2.0.0 for cryptographically random, thread-safe
generation on .NET 10. Keep strongly typed `readonly record struct` wrappers in
Mixology.Kernel and translate them to cedar-dotnet entity UIDs only inside the
authorization adapter.

KsuidDotNet intentionally exposes a generation-focused API. The kernel owns the
small validation boundary needed by public parsing: exact length, base62
alphabet, and a checked 160-bit decode. That preserves interoperability with Go
KSUIDs without letting raw strings or Cedar types leak through every domain.

ULID and UUIDv7 are good .NET identifiers but would change existing CLI values,
seed data, persisted references, cursor order, and cross-language interop. The
older `Ksuid` package includes parsing but has not shipped since 2022 and brings
an older Base62 dependency; it is not selected for a new .NET 10 foundation.

## Validation

- Every generated ID parses through both its typed parser and prefix inference.
- Wrong/unknown prefixes, empty values, malformed alphabets, short/long suffixes,
  and values outside the 160-bit KSUID range fail as invalid input.
- Value semantics, stable string conversion, ordering, and JSON round trips are
  tested for every entity type.

## Sources

- [KsuidDotNet 2.0.0](https://www.nuget.org/packages/KsuidDotNet/2.0.0)
- [KsuidDotNet source and MIT license](https://github.com/steve-warren/ksuid)
- [Segment KSUID specification](https://github.com/segmentio/ksuid)
- [Older `Ksuid` .NET package](https://www.nuget.org/packages/Ksuid/1.0.0)
