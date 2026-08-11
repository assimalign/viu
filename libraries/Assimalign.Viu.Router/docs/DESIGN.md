# Assimalign.Viu.Router design

## Dependency boundary

The package has two production dependencies: Components supplies `VirtualNode`, `ComponentNode`,
component registration, and authored component contexts; Reactivity supplies the shallow current
route. Core and Browser remain downstream. Consequently matching and memory history can run without
a renderer or host, and Router attaches to mounted applications only through
`ComponentContext.Services` and application composition (`[RTR-1]`, `[RTR-7]`, `[CMP-33]`).

Browser.Router exists as a separate leaf package so neither Router nor Browser acquires the other's
dependency graph. It owns web and hash history policy, generated JavaScript dispatch, packaged web
assets, and scroll effects. Router publishes the optional `IInitializableRouterHistory` and
`IRouterScrollController` capability contracts that any history host may implement, and the host
mount signal on `Router`; Router grants no downstream shipping assembly friend access (`[RTR-3]`,
`[RTR-7]`, `[RTR-10]`).

State construction and path normalization remain implementation policy rather than public helper
APIs. Memory history keeps its internal arithmetic in Router, while Browser.Router owns local
internal equivalents. Their shared observable entry/base semantics are pinned by `[RTR-3]` and the
two history test suites, so the package boundary does not expose mutable implementation machinery.

## Immutable route output

A route record stores an eager `VirtualNode?` or an asynchronous factory that returns a
`ComponentNode`; neither path is an activation callback. Matching therefore produces a description
and never mounts a component. A `ComponentNode` continues to carry a
`ComponentReference`, immutable `ComponentInvocation`, key, mount reference, and `RenderPlan`; Core
resolves and activates it later (`[CMP-7]`).

`RouterView` treats that node as an immutable copy boundary. Route-derived arguments override
authored arguments, while every other invocation channel is retained. The view does not mutate the
record's node or resolve its registration. Record identity participates in the effective key so
different route records cannot accidentally share mounted state; the authored key still
distinguishes intentional variants within one record.

Non-component nodes pass through unchanged. This permits redirect placeholders, static content, or
other host-neutral tree descriptions without manufacturing a component wrapper.

## Component integration

`RouterView` and `RouterLink` expose static reflection-free `ComponentRegistration` values and
implement `IComponent.Setup(ComponentContext)`. `RouterResolution` consults nullable services only;
there is no context cast, friend access, or bridge interface (`[CMP-8]`, `[CMP-33]`, `[RTR-4]`).

The view's depth is an explicit declared parameter. That preserves the component model's decision
that parent-to-child data uses parameters and slots rather than an ambient component-tree dependency
API (`[CMP-24]`). `RouterLink` similarly emits a host-neutral click value. It owns navigation
eligibility and router calls, while a host adapter owns conversion from its native event shape.

## Lazy resolution, navigation, and lifetime

Navigation follows an ordered state transition: resolve the target, classify duplication, run global
and record guards, commit history, publish `CurrentRoute`, then notify after-navigation hooks.
Redirects restart resolution with loop protection. Every non-success result is represented by the
typed failure model in `[RTR-5]` and `[RTR-6]`.

The resolve step awaits each matched record's `RouteComponentFactory` after per-record
`BeforeEnter` and before component-associated enter guards. The factory returns the same immutable
`ComponentNode` currency used by eager records, so Core still activates through `IComponentFactory`.
Only success is retained; failure reaches `OnError`, leaves route and history untouched, and allows
the next navigation to invoke the factory again (`[RTR-8]`).

`ScrollBehavior` is host-free and asynchronous. After every confirmation, Router reports the
navigation through the public scroll capability, including when no behavior is currently configured;
that confirmation lets a host invalidate deferred work from an older route. Browser.Router waits for
the post-render flush before applying one effect. Back/forward reads the arriving history state's
saved position. Memory history implements no scroll controller and therefore no-ops (`[RTR-9]`).

Web and hash history need asynchronous module initialization, so no constructor blocks on
JavaScript. `ReadyAsync` completes initialization and the initial navigation before an application
mounts routed content. A Router borrows its `IRouterHistory`: dispose the Router first to remove its
subscription, then dispose the history to release the environment listener. History disposal is
terminal and idempotent. Application lifetime ordering is supplied by Browser.Router, not by this
package.

## AOT and WASM seams

Router contains no browser interop. Lazy factories return statically typed component requests and
never activate a string type name. No reflection-based marshalling, runtime serialization,
constructor probing, or dynamic code generation is permitted (`[RTR-8]`, `[RTR-10]`, `[EXE-4]`).

The .NET 10 WebAssembly SDK can emit a statically declared `BlazorWebAssemblyLazyLoad` item into
the lazy boot-resource manifest for a plain WebAssembly application. The plain runtime does not,
however, expose a supported public loader: the managed service belongs to Blazor and the JavaScript
entry point is an undocumented internal API. Router therefore uses an in-application typed
`Task<ComponentNode>` source and deliberately wraps neither path. This separates manifest support
from supported invocation and preserves the linker/AOT boundary (`[RTR-11]`).

## Non-goals

Router does not own component activation, mounted state, rendering, DOM event policy, application
shutdown, dependency disposal, browser history, selector lookup, or scroll application
(`[CMP-9]`, `[RTR-10]`).
