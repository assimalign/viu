# Viu

A faithful re-implementation of [Vue.js 3](https://vuejs.org) in C#/.NET, running in the browser
through the .NET WebAssembly build tools (`Microsoft.NET.Sdk.WebAssembly`, `JSImport`/`JSExport`
interop) — compiler-informed virtual DOM, Ref-first reactivity, and Roslyn source generators where
Vue uses JS `Proxy` and runtime template compilation.

## Status

Early development. The current code proves the rendering seam: a platform-agnostic virtual DOM
renderer (`VirtualDomRenderer<TNode>` over an injected adapter) patching the real browser DOM from
C# through a handle-based JS-interop bridge. See the stopwatch demo in
[`examples/Assimalign.Viu.WebApp`](examples/Assimalign.Viu.WebApp).

Libraries use the `Assimalign.Viu.*` package root with the inverted layout
`libraries/Assimalign.Viu.<Name>/{src|test}`.

Viu apps consume the framework through an MSBuild project SDK — a complete app csproj is
`<Project Sdk="Assimalign.Viu.Sdk">` — which chains `Microsoft.NET.Sdk.WebAssembly` and delivers
the framework libraries plus the `[Reactive]`/`.viu` source generators via the
`Assimalign.Viu.App` shared framework (`.Ref` targeting pack + `.Runtime.browser-wasm` runtime
pack). See [`sdks/README.md`](sdks/README.md).

## Plan and tracking

- [Delivery plan](docs/PLAN.md) — architecture mapping (Vue 3 package → Viu library), founding
  design decisions, and the wave strategy
- [Project board](https://github.com/orgs/assimalign/projects/15) — the authoritative backlog
  (`[V01.01.*]` WBS items: program → area epics → features → tasks)
- Work-item intake: [`.claude/skills/viu-work-items`](.claude/skills/viu-work-items/SKILL.md)

## Build

```sh
dotnet build Assimalign.Viu.slnx
dotnet run --project examples/Assimalign.Viu.WebApp
```

## License

See [LICENSE](LICENSE).
