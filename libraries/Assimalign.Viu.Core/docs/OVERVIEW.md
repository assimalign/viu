# Assimalign.Viu.Core

The host-neutral application and runtime integration layer. It references Components, Reactivity,
and State while retaining the existing `Assimalign.Viu` root namespace exception.

The application composition boundary:

- The application receives a root `IComponent`.
- The application receives an already-composed `IComponentFactory`.
- The application receives an independent `IServiceProvider`.
- A custom implementation may use one object for both roles, but the contracts do not require it.
- Core borrows both resolvers and does not dispose them.
- Core owns each per-mount template returned by the factory and disposes it on setup failure or
  unmount when it implements `IDisposable`.
- Viu does not create dependency-injection scopes automatically; a custom factory may bind a scope
  to its returned template.
- State is the Store-package replacement and remains optional on an application context.
- Core has no service container, registration, lifetime, or constructor-activation API.
- The application-level error handler is the terminal sink for observed lifecycle and event-task
  faults that are not captured by an ancestor component.

## Component parameter, event, and fallthrough semantics

Core partitions each template request against the activated template's explicit metadata:

- Parameter declarations remain canonical in `IComponentContext.Arguments`, while parent renders
  may use either camel-case or kebab-case names.
- Parameter default factories run at most once per mounted instance. Required and validator failures
  warn without rewriting the supplied value.
- Declared `onX` and `onXOnce` listeners are consumed as component-event listeners. Undeclared
  listeners remain fallthrough attributes, matching Vue's `$attrs` distinction.
- `Emit` carries zero or more ordered arguments. Kebab-case emissions match camel-case listeners,
  event validators inspect the complete argument list, and both ordinary and `Once` listeners may
  run for one emission. Once state belongs to the mounted instance and survives parent updates.
- Single-payload listeners receive the first argument (or null for an argument-free emission).
  All-arguments listeners receive the complete list. Every task returned by a multicast
  asynchronous listener is observed through the component error pipeline.

When attribute inheritance is enabled and the template renders one element root, Core merges
fallthrough properties through the same class, style, and event rules used by `mergeProps`: classes
are space-joined, style declarations merge with the parent value winning, and compatible event
delegates combine in root-then-parent order. Declared component-event listeners never enter this
host-event merge. These rules mirror Vue's [fallthrough attribute
behavior](https://vuejs.org/guide/components/attrs.html) without introducing provide/inject or a
service-container dependency.

## Host-neutral renderer foundation

`Renderer<TNode>` and `RendererOptions<TNode>` provide the host-neutral rendering engine.
Browser, WebView2, tests, and future hosts supply explicit node-operation delegates; Core contains
no browser handles or interop. The renderer supports every unified-tree kind: element, text,
comment, static, fragment, template, and teleport. Its behavior includes:

- first mount, immutable-tree patching, and root unmount;
- keyed reconciliation with a longest-increasing-subsequence move pass;
- compiler-selected positional reconciliation for `UnkeyedFragment`;
- fragment and static-content ranges with insertion anchors and range moves;
- full and patch-flag-selected immutable attribute diffs; and
- `ComponentOptimization.DynamicChildren` patching through a per-mounted-tree reference-identity
  map;
- user-template activation through the application-selected `IComponentFactory`, automatic
  AOT-safe activation of Core built-ins, one reactive render effect and scope per mount,
  lifecycle/error routing, fallthrough attributes, emitted events, and template references;
- stable/dynamic/forwarded slot update gating and comment-only slot fallback;
- per-tree-value `onVnode*` lifecycle hooks without leaking them to the host attribute layer;
- runtime-directive resolution through Vue's raw, camel-case, then Pascal-case asset-name lookup,
  plus all Vue element directive phases;
- teleport movement between logical and target containers, Vue 3.5 deferred target setup,
  target-later-in-the-same-tick resolution, and block dynamic-child patching with static host
  carry-forward;
- server-markup hydration with mismatch recovery, fragment/teleport anchors, cached-block handling,
  and semantic class/style comparison;
- host-neutral `BaseTransition` insertion/removal choreography;
- KeepAlive activation, deactivation, filtering, and least-recently-used eviction; and
- Suspense pending-branch storage, fallback ownership, nested boundary accounting, and coordinated
  reveal.

Public `IComponent` values remain immutable descriptions. Internal sealed
`MountedRenderNode<TNode>` variants own host nodes, ranges, and child bookkeeping. In particular,
`DynamicChildren == null` means a normal full walk while a non-null empty list is an optimized
block that skips all child visits. A mismatched old/new block shape safely falls back to a full
child diff. Block teardown likewise visits collected dynamic descendants only; `HasOnce`, bailed
trees, and keyed or unkeyed fragment blocks retain the full walk required for lifecycle, reference,
directive, and external-target cleanup.

`MountedTemplateNode<TNode>` owns the activated template, component context, reactive render
effect, and mounted subtree; that state never returns to the immutable authoring model. KeepAlive
and Suspense extend this mounted state with detached host containers while retaining the same
generic renderer operations. Suspense hydration intentionally fails fast until pending client
branches can be coordinated with server output; ordinary templates, including awaited
server-prefetched asynchronous components, hydrate through the normal path.

`Application<TNode>` and `IApplication<TNode>` keep the mount target generic. Browser is one host
adapter over that contract, not a dependency of Core, so a later WebView2 host can provide its own
node handles, renderer operations, initialization, and teardown.

## Dynamic, asynchronous, and cached trees

Dynamic selectors and asynchronous definitions retain explicit `IComponentFactory` activation,
deduplicate concurrent loads, and integrate with server prefetch and Suspense. See
[Asynchronous and dynamic components](ASYNCHRONOUS-AND-DYNAMIC-COMPONENTS.md).

`KeepAlive` moves inactive keyed template subtrees into renderer-owned storage while preserving
their component instances and reactive scopes. It implements component-name filtering, reactive
cache pruning, least-recently-used eviction, and child-before-parent activation callbacks. See
[KeepAlive](KEEP-ALIVE.md).

## Host-specific TransitionGroup bridge

`ComponentTransitionScope` attaches one shared transition state to multiple immutable children and
can finish a pending enter phase before a host performs layout measurement.
`ComponentHost.GetKeyedChildElements<TNode>` exposes an ordered snapshot of direct keyed children
and their first mounted host elements. During `OnBeforeUpdate` it observes the outgoing tree; during
`OnUpdated` it observes the patched incoming tree. Browser owns rectangle batching, transforms,
reflow, CSS classes, and transition-end listeners; Core remains unaware of DOM concepts.
