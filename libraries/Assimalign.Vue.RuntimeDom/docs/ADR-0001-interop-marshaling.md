# ADR-0001: Int-handle node identity over `JSObject` proxy marshaling

- **Status:** Accepted (2026-07-17)
- **Work item:** [V01.01.04.01] (#39)
- **Scope:** How DOM nodes cross the JS-interop boundary in `Assimalign.Vue.RuntimeDom`.

## Context

Every renderer node-op crosses the WASM/JS boundary, and the boundary is the framework's dominant
performance cost. .NET offers two ways to identify a DOM node across it:

1. **Int handles** — the JS side keeps a `Map<int, Node>` registry; .NET sees opaque `int`s.
   Signatures stay primitive, which the command buffer ([V01.01.04.05]) requires: an op stream of
   `(opcode, int, string)` tuples can be flattened into one interop call.
2. **`JSObject` proxies** — elements marshal as proxy references. No registry, natural JS-side
   ergonomics, but every node costs a GC-tracked proxy object on both sides, and each proxy must be
   `Dispose()`d from .NET for deterministic release.

## Measurement

Measured with the repeatable harness in the example app (`/?diagnostics=1`,
`examples/Assimalign.Vue.WebApp/VuecsDiagnostics.cs`), identical loops per strategy against the
same live browser DOM. Dev build (interpreted mono-wasm, no AOT), Chromium, 2026-07-17; the
tracked benchmark suite that will re-measure this under AOT is [V01.01.11.04] (#88).

| Op mix | int handle | `JSObject` proxy | ratio (obj/int) |
| --- | --- | --- | --- |
| createElement + teardown (×3,000 pairs) | 2.72 µs/op | 5.43 µs/op | **2.00×** |
| setElementText on one element (×10,000) | 5.23 µs/op | 3.38 µs/op | 0.65× |
| setAttribute on one element (×10,000) | 3.83 µs/op | 3.99 µs/op | 1.04× |
| tree build + teardown (100 × 30 children) | 5.42 µs/op | 6.39 µs/op | **1.18×** |

Reading the numbers honestly:

- **Node creation/destruction — the renderer's bread and butter — is 2× cheaper with handles.**
  The `JSObject` cost is proxy allocation + GC registration + `Dispose`, paid once per node.
- Repeated ops on an *existing* element are roughly equal (attribute 1.04×), and the int-handle
  `setElementText` is *slower* (0.65×) — but that gap is **our contract, not the marshaling**: the
  bridge's `setElementText` also releases replaced-child handles and returns the released-handle
  array (the deterministic-lifecycle contract). The proxy variant does a bare `textContent` write.
- Mixed tree workloads net out ~1.2× in favor of handles even with that contract overhead.

## Decision

**Int handles.** Rationale, in order:

1. **Command-buffer compatibility** ([V01.01.04.05]) is the strategic constraint: primitive-typed
   ops serialize into a batched opcode stream; `JSObject` references cannot.
2. Renderer workloads are creation-heavy, where handles win 2×.
3. Deterministic lifecycle is simpler with one owner: the JS registry releases a removed subtree's
   handles and DOM listeners in the removal call and reports them to .NET, which purges its
   listener delegates in the same call — no per-node `IDisposable` discipline threaded through the
   renderer, no finalizer pressure.
4. The one mix where proxies win (`setElementText`) is priced by our lifecycle contract and is
   precisely the op the command buffer will batch anyway.

## Consequences

- Handle 0 is the reserved "no node" sentinel; the JS side must never issue it.
- `parentNode`/`nextSibling` can register *foreign* nodes (not created by the bridge); they are
  released like any other when their subtree is removed.
- The `int[]` released-handles return on `remove`/`setElementText` is the price of two-sided
  cleanup; revisit its shape (count + shared buffer) with the command buffer.
- Re-measure under AOT with [V01.01.11.04] before optimizing further; dev-build ratios are
  indicative, not final.
