# Assimalign.Viu.Testing — design

Why the testing package is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md). Upstream
counterparts: [`@vue/runtime-test`](https://github.com/vuejs/core/tree/main/packages/runtime-test)
and [`@vue/test-utils`](https://test-utils.vuejs.org).

## A second platform for the one renderer

`Assimalign.Viu.Core`'s renderer is platform-agnostic: it drives whatever
`RendererOptions<TNode>` it is given (see
[`Assimalign.Viu.Core/docs/DESIGN.md`](../../Assimalign.Viu.Core/docs/DESIGN.md)).
`TestNodeOperations.Create(log, teleportTargetRoots?)` supplies node-ops over a plain in-memory tree
(`TestElement`/`TestText`/`TestComment`), so component behavior is exercised through the *same*
mount/patch/unmount pipeline the browser uses — just with no DOM and no interop. This is exactly
`@vue/runtime-test`'s role, and it is why a component tested here behaves as it will in the browser.

The one platform op the browser has that a bare in-memory tree lacks is `querySelector` — which the
renderer uses only to resolve a `<Teleport>` string target ([V01.01.03.17]). The test adapter supplies
it as a search over the registered *target roots* (each render container is auto-registered;
`TestRenderer.RegisterQueryRoot` adds a detached target container), matching against the same CSS subset
`@vue/test-utils` accepts (tag/`#id`/`.class`/`[attr]`). This keeps Teleport exercised DOM-free — a
selector target rendered later in the same tree resolves through the ordinary tree walk, so `defer`
behaves as it does in the browser. A renderer built without target roots simply declares no
`querySelector`, so a string Teleport target then warns as unsupported (as an SSR string renderer would).

## The op log is the assertion surface

Every node operation the renderer issues lands in `TestNodeOperationLog` as a `TestNodeOperation`
(`@vue/runtime-test`'s `nodeOps` op log). Tests assert *what the renderer did* — which inserts,
removes, and text writes happened, in order — not only the final tree. `TestNodeSerializer` renders
the tree to a string for snapshot-style assertions. Container creation is intentionally **not**
logged, so the log isolates the renderer's own work.

## Determinism is owned by the mount

`ViuTest.Mount` takes ownership of the scheduler lifecycle for the mount: it resets the `Scheduler`
and installs a `TestSchedulerPump` so flushes are captured and async update helpers are
reproducible, with no reliance on an ambient `SynchronizationContext`. The returned `ComponentWrapper`
is `IDisposable`; disposing it (a `using`) unmounts and returns the scheduler to baseline, so one
test cannot leak reactive subscriptions or queued jobs into the next.

## Deltas from Vue 3

- **No jsdom.** `@vue/test-utils` normally mounts into a jsdom DOM; Viu's platform here is a pure
  in-memory node tree, keeping the whole harness dependency-light and AOT-clean.
- **Component instances are caller-supplied**, constructed by source-generated factories — never
  reflection-activated — so the harness stays trimming/AOT-safe.

## Non-goals (sequenced work)

- The end-to-end browser test harness — [V01.01.11.03].
- The performance benchmark suite — [V01.01.11.04].
