---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# General rules (C#)

These are the canonical coding conventions for Vuecs. They load automatically when a `.cs`/`.csproj`
file is touched — do not re-derive conventions from scratch. Vuecs is a faithful re-implementation of
Vue.js 3 in C#/.NET WebAssembly; where behavior mirrors Vue, **upstream vuejs/core (v3.5.x) semantics
win** — link the reference in the code, test, or issue that pins the behavior.

## Project layout

- Inverted library layout: `libraries/Assimalign.Vue.<Name>/{src|test}` — the folder name **is** the
  assembly / package id. `src/` holds the shipping project, `test/` its test project. No area wrapper
  folders. Package root is `Assimalign.Vue.*` (the product/repo name stays "Vuecs").
- Examples live in `examples/`; repo planning docs in `docs/`.

## Namespaces

- **File-scoped** namespace declarations (`namespace X;`).
- **Namespace == assembly name**, flat. Every file in `Assimalign.Vue.Reactivity` declares
  `namespace Assimalign.Vue.Reactivity;` regardless of subfolder. `Abstraction/` and `Internal/` are
  **physical folders only** — they never appear in a namespace.

## Folders within `src/`

- **Public interfaces** → `src/Abstraction/` (flat).
- **Internal types** (classes, structs, enums, records, **and internal interfaces**) → `src/Internal/` (flat).
- **Delegates** (public delegate declarations) → `src/Delegates/`.
- **Public non-interface types** group into **feature folders** (`Rendering/`, `Components/`, `Watch/`, `Blocks/`, …): one folder per coherent feature set. Types used across the whole library (the "currency" types — e.g. `VirtualNode`, the flag enums, a library's facade) stay at the `src/` root.
- Folders are **physical only** — they never appear in a namespace. Create a folder only when it will contain files.
- Linked shared-source files (`PatchFlags.cs`, `SlotFlags.cs`, `Internal/DomKnowledgeData.cs` from `Assimalign.Vue.Shared`; `Shims/IsExternalInit.cs`, `Shims/RequiredMemberShims.cs` from `Assimalign.Vue.Syntax`) are `<Compile Include>` targets from netstandard2.0 projects — **their paths are frozen**; moving them requires updating every linking csproj in the same change.

## Files and types

- **One public type per file**; the filename is the type name.
- Generic types use `{T}` in the filename: `IReference<T>` → `IReference{T}.cs`. Do **not** use `OfT`
  or similar suffixes in type names or filenames.
- Group a variant family root-first when splitting (`VirtualDomPatch.cs` + one file per record).

## Naming — spell out whole words

- **No abbreviations.** `Ref` → `Reference`, `Dep` → `Dependency`, `Sub` → `Subscriber`, `Ops` →
  `Operations`, `Prev` → `Previous`, `Prop`/`Props` → `Property`/`Properties`. This applies to types,
  members, parameters, and locals.
- **Well-known acronyms stay acronyms**: DOM, HTML, CSS, SSR, AOT, JSON, WASM (e.g. `IVirtualDomAdapter`,
  `HtmlRenderer`). The approved list is exactly those seven; nothing else is treated as an acronym.
  **SFC is _not_ on the list** — identifiers spell out `SingleFileComponent` (the
  `Assimalign.Vue.Syntax.SingleFileComponent` area), never `Sfc`. Prose may still write "single-file
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
  externally (see `Assimalign.Vue.Reactivity`'s `Subscriber`).
- **Single-threaded model**: the runtime targets the JS event loop. Ambient `static` state is acceptable,
  but any non-thread-safe type must say so in its XML docs.

## AOT / trimming (hard constraints)

- Trimming- and WASM/NativeAOT-safe: **no reflection-based serialization, no dynamic code generation, no
  linker-unfriendly activation paths.** Roslyn **source generators** are the sanctioned path for anything
  Vue does with `Proxy` or runtime `new Function`.
- Shipping libraries set `<IsAotCompatible>true</IsAotCompatible>` (see [build-system.md](build-system.md)).
- The JS-interop boundary is the dominant performance cost — batch interop, and always clean up JS-side
  handles and event listeners.
