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
mount/hydrate/patch/unmount pipeline the browser uses — just with no DOM and no interop. This is
exactly `@vue/runtime-test`'s role, and it is why a component tested here behaves as it will in the
browser.

`RegisterQueryRoot` retains detached roots for `RendererOptions.ResolveTeleportTarget`. A string
target searches each registered root and its descendants with the same tag, identifier, class, and
attribute selector subset used by wrapper queries. Render containers are registered automatically;
detached targets are explicit. Direct `TestNode` targets bypass lookup through Core's generic target
path.

## The op log is the assertion surface

Every node operation the renderer issues lands in `TestNodeOperationLog` as a `TestNodeOperation`
(`@vue/runtime-test`'s `nodeOps` op log). Tests assert *what the renderer did* — which inserts,
removes, and text writes happened, in order — not only the final tree. `TestNodeSerializer` renders
the tree to a string for snapshot-style assertions. Container creation is intentionally **not**
logged, so the log isolates the renderer's own work.

## Hydration has live and frozen readers

`TestServerMarkup.Parse` creates the host tree a browser would normally produce by parsing server
HTML. `TestHydrationReader` walks that tree live. This is the simplest host implementation and
supports adoption of elements, text, fragment marker ranges, template subtrees, and registered
Teleport targets. Event attributes still cross the host boundary during hydration so an adopted
element becomes interactive.

`FrozenTestHydrationReader` captures the complete container or Teleport-target subtree when Core
asks for a reader. `new TestRenderer(snapshotSemantics: true)` selects it and also enables strict
duplicate-removal checks. The frozen topology deliberately remains readable after host mutations,
matching a browser or WebView2 adapter that obtains one batched interop snapshot before walking it.
Range-recovery tests therefore catch algorithms that remove an opening marker before collecting
the fragment or Teleport range that follows it.

Both readers feed Core's host-neutral hydration walker; Testing does not contain a second
component reconciliation algorithm. A hydrated reactive template owns the adopted nodes and later
updates them through the ordinary patch path.

## Template mounts use the application contracts

Both `ViuTest.Mount` paths can create a real `ApplicationContext`. The authored-template overload
also creates its root template request. The internal test factory returns the exact caller-supplied
root instance once and delegates every other activation to the optional application-selected
`IComponentFactory`. A tree mount uses the same factory without a special root, allowing an
otherwise primitive root to contain template children. This keeps activation explicit and AOT-safe
while allowing assertions against a supplied root instance.
Type-keyed test stubs are resolved before the delegated child factory. Each explicit stub activator
creates a fresh template; a null activator creates a recognizable placeholder element.

The application borrows `ComponentMountOptions.Services`, `State`, and child component factory.
Testing does not dispose those application-owned objects. Core still owns the template returned for
the root mount and disposes it after unmount when it implements `IDisposable`.

Component dependencies use `IComponentContext.Services`; the removed component-tree provide/inject
model is intentionally absent. Arguments, slots, parent event listeners, error/warning handlers,
the state-registry bridge, and the directive resolver therefore travel through the same public
contracts a platform application uses. The test host also stamps template scope identifiers onto
its element attributes, so scoped-style ownership is visible in serialized output and the operation
log.

## Child wrappers use read-only mounted-template inspection

Core keeps renderer ownership internal while granting this testing assembly friend access to a
depth-first, read-only mounted-template snapshot. `FindComponent` filters that snapshot to
descendants of the current wrapper, then scopes host queries and serialization to the selected
template's first-to-last host range. Child wrappers borrow the root wrapper's renderer and scheduler;
disposing or unmounting a child wrapper does not tear down the application root.

## Emitted-event capture uses component identity

The concrete `ApplicationContext` exposes a friend-only event observer invoked before ordinary
parent-listener dispatch. Testing records by the emitting `IComponentContext`, so:

- setup-time and later emits are captured;
- declared and undeclared event names are captured;
- root and child wrapper histories remain separate;
- supplied synchronous and asynchronous parent listeners remain unchanged, with Core retaining
  error and task observation.

## Determinism is owned by the mount

`ViuTest.Mount` takes ownership of the scheduler lifecycle for the mount: it resets the `Scheduler`
and installs a `TestSchedulerPump` so flushes are captured and async update helpers are
reproducible, with no reliance on an ambient `SynchronizationContext`. The returned `ComponentWrapper`
is `IDisposable`; disposing it (a `using`) unmounts and returns the scheduler to baseline, so one
test cannot leak reactive subscriptions or queued jobs into the next.

`Trigger` and `SetValue` first await a task-returning host event handler, then drain the scheduler.
Lifecycle and component-event tasks are observed by Core rather than awaited as phase barriers, so
tests that control those tasks should complete them and then call `NextTickAsync` or `FlushAsync`.

## Deltas from Vue 3

- **No jsdom.** `@vue/test-utils` normally mounts into a jsdom DOM; Viu's platform here is a pure
  in-memory node tree, keeping the whole harness dependency-light and AOT-clean.
- **The root template instance is caller-supplied.** Child templates still use the configured
  `IComponentFactory`; neither path uses reflection activation.
- **Mounted child traversal is testing-only.** Applications still do not receive mutable mounted
  renderer state; the friend seam exists only so wrappers can provide test-utility queries.
- **Snapshot hydration is selectable.** The ordinary test renderer uses a live tree; tests that need
  to model a batched browser/WebView2 read opt into the frozen reader explicitly.

## Non-goals (sequenced work)

- The end-to-end browser test harness — [V01.01.11.03].
- The performance benchmark suite — [V01.01.11.04].
