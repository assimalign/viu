---
paths:
  - "**/test/**"
  - "**/*Tests*.cs"
---

# Testing

- **xUnit v2 + Shouldly** are the sanctioned frameworks. Shouldly is the single assertion library — do not
  add FluentAssertions or lean on raw `Assert`. Package versions come centrally
  ([build-system.md](build-system.md)); the test csproj declares them by name via `ViuPackageReference`.
- Each library has a sibling test project at `libraries/<Area>/Assimalign.Viu.<Name>/test/`
  (`Assimalign.Viu.<Name>.Tests`), `IsPackable=false`, referencing its `src` via `ViuProjectReference`.
  Compiler and editor projects follow the same shape under
  `tooling/<Area>/Assimalign.Viu.<Name>/test/`; the end-to-end harness lives under
  `benchmarks/Assimalign.Viu.Testing.EndToEnd`.
- Class `{Feature}Tests`; method names describe `Method_Scenario_ExpectedBehavior` (or an equally explicit
  phrase). Arrange / Act / Assert.

## What to assert

- Pin **observable behavior**, and for reactivity/caching semantics assert **run counts** (effect runs,
  getter invocations), not just final values — caching and dependency-tracking bugs hide behind
  correct-looking values.
- The test pins **Viu's own specified behavior** — the repository's tests *are* the authority for how
  Viu behaves. Spell the pinned behavior out in the test name or a comment ("an empty
  `DynamicChildren` list skips every child visit"), so a later reader can tell an intentional
  contract from an accidental one, and cite the clause in [`docs/SPECIFICATION.md`](../../docs/SPECIFICATION.md)
  or the `[Vxx.xx.xx]` work item that specified it. Never cite another framework's source or
  documentation as the reason a value is what it is.
- Where a test pins a documented **external compatibility target** — the `.vue` single-file-component
  container format, WHATWG HTML serialization, or the Language Server Protocol — name and link that
  target. There the citation *is* the requirement: the test asserts conformance to a foreign format
  Viu deliberately consumes. Tests inside the standalone utility-CSS add-on may still cite Tailwind CSS
  v4.3.3 as that add-on's target; this does not make Tailwind a Viu core compatibility target.
- Cover exception paths (throwing effects/getters, teardown under error) and lifecycle edges (stop,
  dispose, scope teardown), not just the happy path.

## DOM-free by default

- Unit tests must not require a browser. Exercise the runtime through an in-memory adapter/renderer (the
  Core `FakeDomAdapter` today; the shipping `Assimalign.Viu.Testing` renderer once
  [V01.01.11.01] lands). Real-browser coverage is the separate
  `benchmarks/Assimalign.Viu.Testing.EndToEnd` harness ([V01.01.11.03]).
- Use `InternalsVisibleTo` (in `src/Properties/AssemblyInfo.cs`) for tests that probe internal engine
  state.
