# Vuecs

A faithful re-implementation of [Vue.js 3](https://vuejs.org) in C#/.NET, running in the browser
through the .NET WebAssembly build tools (`Microsoft.NET.Sdk.WebAssembly`, `JSImport`/`JSExport`
interop) — compiler-informed virtual DOM, Ref-first reactivity, and Roslyn source generators where
Vue uses JS `Proxy` and runtime template compilation.

## Status

Early development. The current code proves the rendering seam: a platform-agnostic virtual DOM
renderer (`VirtualDomRenderer<TNode>` over an injected adapter) patching the real browser DOM from
C# through a handle-based JS-interop bridge. See the stopwatch demo in
[`examples/Assimalign.Vue.WebApp`](examples/Assimalign.Vue.WebApp).

Libraries use the `Assimalign.Vue.*` package root with the inverted layout
`libraries/Assimalign.Vue.<Name>/{src|test}`.

## Plan and tracking

- [Delivery plan](docs/PLAN.md) — architecture mapping (Vue 3 package → Vuecs library), founding
  design decisions, and the wave strategy
- [Project board](https://github.com/orgs/assimalign/projects/15) — the authoritative backlog
  (`[V01.01.*]` WBS items: program → area epics → features → tasks)
- Work-item intake: [`.claude/skills/vuecs-work-items`](.claude/skills/vuecs-work-items/SKILL.md)

## Build

```sh
dotnet build Assimalign.Vuecs.slnx
dotnet run --project examples/Assimalign.Vue.WebApp
```

## License

See [LICENSE](LICENSE).
