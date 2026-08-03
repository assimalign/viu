---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# General rules (C#)

These are the canonical coding conventions for Viu. They load automatically when a `.cs`/`.csproj`
file is touched — do not re-derive conventions from scratch.

Viu is a **standalone** C#/.NET WebAssembly UI framework. **[`docs/SPECIFICATION.md`](../../docs/SPECIFICATION.md)
is the authority for Viu's semantics**, and behavior is pinned by tests in this repository — no
external project's behavior, release, or roadmap is authoritative for Viu (decision of 2026-08-02).
Where a type implements a documented **external compatibility target** — the `.vue`
single-file-component container format ([V01.01.06.09], a shipping feature), Tailwind CSS v4.3.3
(Viu Utilities), WHATWG HTML serialization, the Language Server Protocol — name and link that
target. That is a compatibility *requirement* on a foreign format, not a semantic authority over
Viu.

## Project layout

- Inverted library layout: `libraries/Assimalign.Viu.<Name>/{src|test}` — the folder name **is** the
  assembly / package id. `src/` holds the shipping project, `test/` its test project. No area wrapper
  folders. Package root is `Assimalign.Viu.*` (product name "Viu"; the GitHub repo slug is
  `assimalign/viu`).
- Examples live in the separate sibling `viu-examples` repository; repo planning docs live in
  `docs/`; the consumer-facing MSBuild SDK lives in `sdks/` and the `Assimalign.Viu.App`
  shared-framework pack producers live in `frameworks/` (see [build-system.md](build-system.md)).

## Namespaces

- **File-scoped** namespace declarations (`namespace X;`).
- **Namespace == assembly name**, flat. Every file in `Assimalign.Viu.Browser` declares
  `namespace Assimalign.Viu.Browser;` regardless of subfolder. `Abstraction/` and `Internal/` are
  **physical folders only** — they never appear in a namespace.
- **Recorded exception (origin [V01.01.12.21] R2; retained through the 2026-07 redesign, see
  [V01.01.11.04.02] #251):** `Assimalign.Viu.Core` roots every type at the **`Assimalign.Viu`**
  namespace (set via `<RootNamespace>Assimalign.Viu</RootNamespace>` on its `src` csproj), *not*
  `Assimalign.Viu.Core`, because the core **is** the product and its primitives read best unprefixed
  (`Assimalign.Viu.Scheduler`, `Assimalign.Viu.TemplateReference`). Note the R2 *consolidation* this
  exception originally shipped with was superseded: the finalized redesign deliberately re-split
  `Assimalign.Viu.Reactivity`, `Assimalign.Viu.Components`, and `Assimalign.Viu.State` into separate
  libraries, each keeping namespace == assembly id (`Assimalign.Viu.Reactivity.Reference<T>`). The
  root-namespace deviation survives for `Assimalign.Viu.Core` alone; every other library keeps
  namespace == assembly id (the source-generator assemblies included).

## Folders within `src/`

- **Public interfaces** → `src/Abstraction/` (flat).
- **Internal types** (classes, structs, enums, records, **and internal interfaces**) → `src/Internal/` (flat).
- **Delegates** (public delegate declarations) → `src/Delegates/`.
- **Public non-interface types** group into **feature folders** (`Rendering/`, `Components/`, `Watch/`, `Blocks/`, …): one folder per coherent feature set. Types used across the whole library (the "currency" types — e.g. `VirtualNode`, the flag enums, a library's facade) stay at the `src/` root.
- Folders are **physical only** — they never appear in a namespace. Create a folder only when it will contain files.
- Linked shared-source files (`PatchFlags.cs`, `SlotFlags.cs`, `Internal/DomKnowledgeData.cs` from `Assimalign.Viu.Shared`; `Shims/IsExternalInit.cs`, `Shims/RequiredMemberShims.cs` from `Assimalign.Viu.Syntax`) are `<Compile Include>` targets from netstandard2.0 projects — **their paths are frozen**; moving them requires updating every linking csproj in the same change.

## Files and types

- **One public type per file**; the filename is the type name.
- Generic types use `{T}` in the filename: `Store<TState>` → `Store{TState}.cs`. Do **not** use `OfT`
  or similar suffixes in type names or filenames. (A root+generic split family may instead use the
  dotted `.T.cs` form, e.g. `ReactiveValue.cs` + `ReactiveValue.T.cs`, matching its siblings.)
- Group a variant family root-first when splitting (`VirtualDomPatch.cs` + one file per record).

## Naming — spell out whole words

- **No abbreviations.** `Ref` → `Reference`, `Dep` → `Dependency`, `Sub` → `Subscriber`, `Ops` →
  `Operations`, `Prev` → `Previous`, `Prop`/`Props` → `Property`/`Properties`. This applies to types,
  members, parameters, and locals.
- **Well-known acronyms stay acronyms**: DOM, HTML, CSS, SSR, AOT, JSON, WASM (e.g. `IVirtualDomAdapter`,
  `HtmlRenderer`). The approved list is exactly those seven; nothing else is treated as an acronym.
  **SFC is _not_ on the list** — identifiers spell out `SingleFileComponent` (the
  `Assimalign.Viu.Syntax.SingleFileComponent` area), never `Sfc`. Prose may still write "single-file
  component (SFC)".
- Interfaces begin with `I` (editorconfig-enforced at **error** severity).

## Using directives

- **Explicit usings only** — implicit/global usings are disabled repo-wide. Every file declares what it
  uses.
- Order: `System.*` (sorted) → third-party → `Assimalign.*`, then a blank line before the namespace.
  Usings sit **outside** the namespace.

## Design

- **Interface-first**: the public contract is an interface under `Abstraction/`; prefer `internal`
  concrete implementations (surfaced through the interface or a public facade like `Reactive`).
- **Dispatch on hot paths**: interfaces are for public contracts and cold paths. On the engine's hot
  paths (per-trigger notification, patching, diffing) prefer an **abstract base class** over an
  interface — .NET interface dispatch is measurably costlier than a vtable virtual call, and the gap
  widens on mono-wasm / NativeAOT. Put shared per-instance state on the base as fields (direct loads,
  no property-getter dispatch); `seal` concrete leaf types so the JIT can devirtualize. When a public
  type must derive from an otherwise-internal base, make the base a `public abstract` class with
  `internal` members and a `private protected` constructor so it stays opaque and un-subclassable
  externally (see `Assimalign.Viu.Core`'s `Subscriber`).
- **Single-threaded model**: the runtime targets the JS event loop. Ambient `static` state is acceptable,
  but any non-thread-safe type must say so in its XML docs.

## AOT / trimming (hard constraints)

- Trimming- and WASM/NativeAOT-safe: **no reflection-based serialization, no dynamic code generation, no
  linker-unfriendly activation paths.** Roslyn **source generators** are the sanctioned path for every
  form of metaprogramming — reactive property wrappers, component activation, and template
  compilation all happen at build time, never through runtime interception or emitted IL.
- Shipping libraries set `<IsAotCompatible>true</IsAotCompatible>` (see [build-system.md](build-system.md)).
- The JS-interop boundary is the dominant performance cost — batch interop, and always clean up JS-side
  handles and event listeners.
