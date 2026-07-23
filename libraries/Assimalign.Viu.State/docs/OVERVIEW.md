# Assimalign.Viu.State

`Assimalign.Viu.State` is Viu's Pinia-shaped, platform-neutral application-state package. It
replaces `Assimalign.Viu.Store`; there is no second Store package or compatibility implementation
inside the redesign.

The package supports two authoring styles:

- A lightweight setup store may be any object returned by
  `StateStoreDefinition<TStateStore>`. References, computeds, effects, and methods stay ordinary
  Reactivity APIs.
- `StateStore<TState>` is the optional richer member model for developers who need `Patch`,
  `Reset`, `Subscribe`, and `OnAction` over a source-generated `[Reactive]` state object.

`StateStoreRegistry` owns one detached reactive root scope. Each initialized store receives an
attached child scope created while that root is current. This keeps state independent from the
component scope that first resolves it while allowing registry disposal to stop the entire state
subsystem. A definition creates exactly one instance per registry, and a different definition
claiming an existing key raises `DuplicateStateStoreKeyException`.

State receives `IComponentFactory` and `IServiceProvider` independently through `IStateContext`.
The factory remains only a component resolver; State does not assume it implements
`IServiceProvider`. The context also exposes an optional `IReactiveWatchScheduler`:

- Core supplies its application scheduler so direct writes in one application turn deduplicate
  into one pre-flush subscription notification.
- A null scheduler deliberately selects standalone Reactivity's synchronous behavior. A grouped
  `Patch` still produces one notification because the reactive mutations run in one batch.

`IStateStoreContext` is the State-owned capability Core's concrete component context implements.
It lets `definition.Use(componentContext)` locate the application's registry without making
Components reference State or requiring the service provider to duplicate the registry.

State depends only on Components and Reactivity. Construction, state copying, action observation,
and resolution use typed delegates; there is no reflection-based activation or dynamic code.

See [DESIGN.md](DESIGN.md) for the lifetime, subscription, and compatibility decisions.
