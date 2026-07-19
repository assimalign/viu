---
paths:
  - "**/test/**"
  - "**/*Tests*.cs"
---

# Testing

- **xUnit v2 + Shouldly** are the sanctioned frameworks. Shouldly is the single assertion library — do not
  add FluentAssertions or lean on raw `Assert`. Package versions come centrally
  ([build-system.md](build-system.md)); the test csproj declares them by name via `ViuPackageReference`.
- Each library has a sibling test project at `libraries/Assimalign.Viu.<Name>/test/`
  (`Assimalign.Viu.<Name>.Tests`), `IsPackable=false`, referencing its `src` via `ViuProjectReference`.
- Class `{Feature}Tests`; method names describe `Method_Scenario_ExpectedBehavior` (or an equally explicit
  phrase). Arrange / Act / Assert.

## What to assert

- Pin **observable behavior**, and for reactivity/caching semantics assert **run counts** (effect runs,
  getter invocations), not just final values — caching and dependency-tracking bugs hide behind
  correct-looking values.
- Where behavior mirrors Vue 3, the test pins the **upstream contract** — reference the vuejs/core file or
  vuejs.org page in a comment so a divergence is caught, not enshrined.
- Cover exception paths (throwing effects/getters, teardown under error) and lifecycle edges (stop,
  dispose, scope teardown), not just the happy path.

## DOM-free by default

- Unit tests must not require a browser. Exercise the runtime through an in-memory adapter/renderer (the
  RuntimeCore `FakeDomAdapter` today; the shipping `Assimalign.Viu.Testing` renderer once
  [V01.01.11.01] lands). Real-browser coverage is the separate e2e harness ([V01.01.11.03]).
- Use `InternalsVisibleTo` (in `src/Properties/AssemblyInfo.cs`) for tests that probe internal engine
  state.
