# Assimalign.Viu.Router

Router is Viu's navigation package. Its matcher and navigation model are host-free, and the assembly
references only Components and Reactivity. It does not reference Core or Browser. This keeps route
matching usable in a plain .NET process while allowing route output and navigation state to use
Viu's immutable component and reactive contracts (`[RTR-1]`, `[RTR-7]`).

## Routes and matching

`RouteRecord` associates a ranked path pattern with an immutable `VirtualNode`. `RouteMatcher`
tokenizes and ranks records, resolves parameters, and produces value-equal `RouteLocation`
snapshots. Static segments outrank dynamic segments, optional and repeated parameters retain their
declared semantics, and matcher failures use the package's typed error surface (`[RTR-1]`,
`[RTR-2]`).

`Router` coordinates matching, history, guards, and the shallow-reactive `CurrentRoute`. Guards
return an allow, abort, or redirect result; they never receive a continuation. A navigation that is
aborted, superseded, or duplicated returns a typed `NavigationFailure`, while unexpected faults flow
to the configured error handler (`[RTR-5]`, `[RTR-6]`).

## History

`RouterHistory` creates memory, web, and hash histories. Memory history is entirely managed. Web
and hash histories initialize lazily through `Router.ReadyAsync`, marshal only flat primitive state,
and consume the packaged `viu-history.js` module. The package owns that module and its
`buildTransitive` target because the JavaScript exports call the Router assembly's generated
`JSExport` dispatch surface (`[RTR-3]`).

The history state model records navigation position, direction, replacement, and optional scroll
coordinates. It preserves captured state for a host to consume; automatic scroll restoration is not
implemented (`[RTR-8]`).

## Route components

`RouterView` and `RouterLink` are ordinary `IComponent` registrations. They resolve the current
`Router` only through nullable `ComponentContext.Services`, so registration and service ownership
remain with the caller's composition root (`[CMP-9]`, `[CMP-33]`, `[RTR-4]`).

`RouterView` returns a matched non-component `VirtualNode` unchanged. For a matched
`ComponentNode`, it creates a new non-activating mount request, merges route arguments over authored
arguments, and preserves slots, listeners, directives, slot stability, mount reference, render plan,
and component reference (`[CMP-7]`). The effective key combines route-record identity with the
authored key: parameter-only navigation within one record can retain the mount, while navigation to
a different record remounts even when both records use the same component.

`RouterLink` renders a host-neutral anchor node and converts its click binding into a
`RouterLinkClickEvent`. Modified, non-primary, already-prevented, and `target="_blank"` clicks are
left to the host. Browser-specific event translation belongs to `Assimalign.Viu.Browser.Router`
(`[RTR-4]`, `[RTR-7]`).

## Boundaries

Router owns navigation policy, matching, guard ordering, route components, and history state. It
does not mount component trees, patch a host, manage application lifetime, or interpret DOM events.
Route nesting depth is an explicit `RouterView` argument because the component model has no ambient
hierarchical dependency channel (`[CMP-24]`). Lazy route-component loading and automatic scroll
behavior are current limits (`[RTR-8]`).

All activation is registration-based and all browser interop is source-generated. The package uses
no runtime constructor discovery, reflection serialization, emitted code, or dynamic activation
path (`[CMP-6]`, `[EXE-4]`).
