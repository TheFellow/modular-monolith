# Linting, randomized tests, and race detection

## Inherited requirements

The Go reference runs `golangci-lint` with `misspell`, then executes
`go test -race -shuffle=on -count=1`. The semantic port needs the same three
kinds of signal without pretending that unrelated .NET tools are exact
implementations of Go's runtime facilities:

- spell-check prose, source, configuration, and identifiers;
- vary test order on every CI run while preserving a seed for reproduction;
- dynamically detect genuine unsynchronized managed-memory accesses.

All additions must remain open source, version-pinned, locally runnable, and
enforced by the existing validation workflow.

## Options considered

| Concern | Candidate | Assessment |
| --- | --- | --- |
| Spelling | CSpell | MIT-licensed, language-aware, configurable, and supports local CLI use over the whole repository. |
| Spelling | codespell | Mature and simple, but its Python dependency adds a second package ecosystem without better C# awareness. |
| Test order | xUnit v3 stable randomization | Native stable randomization with a reproducible assembly unique ID; already available to the Avalonia test project. |
| Test order | Migrate every test to xUnit v3 | Eventually desirable, but unnecessary for this gate and a materially larger test-platform migration. |
| Test order | xUnit v2 orderers | Its documented case and collection orderer extension points can implement a seeded order without changing the test framework. |
| Concurrency | SharpDetect | Apache-2.0 dynamic analyzer for .NET 10 that instruments managed field accesses and supported synchronization, with a nonzero exit code for findings. |
| Concurrency | Microsoft Coyote | Excellent systematic scheduling for deliberately modeled task-based concurrency, but not a general data-race detector and requires specialized tests or binary rewriting. |
| Concurrency | Repeated/parallel tests | Useful stress, but observes failures rather than conflicting memory accesses and must not be presented as a race detector. |

## Decision

CSpell `10.0.1` is pinned in `package-lock.json`. `cspell.json` applies one
reviewed project dictionary and excludes only generated, vendored, and Git
metadata. CI uses pinned Node.js `22.18.0`, installs with `npm ci
--ignore-scripts`, and runs:

```sh
npm run lint:spelling
```

The remaining xUnit v2 projects compile the shared `eng/XunitRandomOrderers.cs`,
which hashes each test identity with `MIXOLOGY_TEST_ORDER_SEED` and orders both
cases and collections by that key. The desktop and desktop-toolkit projects use
xUnit v3's native stable randomization; an MSBuild target writes the same seed
plus project name to each `.uniqueid` file. The small concurrency-focused
desktop-toolkit project was moved to v3 after its v2 custom-orderer testhost
repeatedly failed to exit under solution-level parallel VSTest orchestration,
while standalone execution always passed. The targeted v3 project completes
under the same orchestration and remains discoverable through VSTest, the
runner used by SharpDetect. Its asynchronous tests also pass xUnit's current
test cancellation token into `LatestRequest`, allowing an interrupted gate to
cancel and drain its accepted work.

CI derives the seed from the workflow run and attempt IDs and prints it before
testing. Reproduce an ordering failure with:

```sh
MIXOLOGY_TEST_ORDER_SEED=123456-1 dotnet build Mixology.slnx
MIXOLOGY_TEST_ORDER_SEED=123456-1 dotnet test Mixology.slnx --no-build
```

SharpDetect `2.1.4` is pinned in the .NET tool manifest. The Linux quality job
runs its FastTrack data-race plugin against the concurrency-focused
`LatestRequestTests`; a finding fails the job. This is the closest practical
open-source equivalent to `go test -race` for this repository, but it is not an
exact equivalent. SharpDetect supports .NET 8-10 on Windows/Linux x64 and
instruments instance/static fields plus a documented set of task, thread, and
synchronization operations. It does not analyze array elements, may miss races
because of publication heuristics, and can report false positives around
unsupported synchronization. Dynamic coverage also remains limited to code
exercised by the selected tests. macOS developers run the gate through CI.

The gate uses `eng/sharpdetect-latest-request.json` rather than inline options.
SharpDetect's configuration format permits assembly-prefix exclusions that the
CLI flags do not expose. The configuration retains instrumentation for
`Mixology.Toolkits.Desktop` while excluding the test assembly, VSTest host,
xUnit, SharpDetect itself, and Newtonsoft.Json. Those exclusions prevent test
runner implementation details from being mistaken for product races without
hiding the production concurrency primitive under test. `LatestRequest<T>`
uses a static task continuation instead of a compiler-generated closure, while
completion metadata is inserted and removed under the same `Monitor` lock.
Both task lifecycle and `Monitor` synchronization are documented by the pinned
SharpDetect release.

## Validation gates

```sh
npm ci --ignore-scripts
npm run lint:spelling
dotnet tool restore
MIXOLOGY_TEST_ORDER_SEED=local-check dotnet build Mixology.slnx
MIXOLOGY_TEST_ORDER_SEED=local-check dotnet test Mixology.slnx --no-build
dotnet sharpdetect run eng/sharpdetect-latest-request.json
```

The final command requires Windows or Linux x64.

## Primary sources

- [CSpell installation and CI guidance](https://cspell.org/docs/installation)
- [CSpell configuration reference](https://cspell.org/docs/Configuration)
- [.NET test-order extension points](https://learn.microsoft.com/en-us/dotnet/core/testing/order-unit-tests)
- [xUnit v3 stable randomization and `.uniqueid`](https://xunit.net/docs/getting-started/v3/whats-new#stable-randomization)
- [SharpDetect source, supported operations, limitations, and platforms](https://github.com/acizmarik/sharpdetect)
- [SharpDetect test-assembly runner](https://github.com/acizmarik/sharpdetect/blob/main/docs/guides/running-analysis-against-tests.md)
- [SharpDetect configuration-file and instrumentation-scope reference](https://github.com/acizmarik/sharpdetect/blob/main/docs/guides/running-analysis-with-configuration-file.md)
- [SharpDetect 2.1.4 package](https://www.nuget.org/packages/SharpDetect/2.1.4)
- [Microsoft Coyote controlled task scheduling](https://microsoft.github.io/coyote/concepts/tasks/overview/)
