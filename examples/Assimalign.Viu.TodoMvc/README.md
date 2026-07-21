# Assimalign.Viu.TodoMvc

The canonical [TodoMVC](https://github.com/tastejs/todomvc/blob/master/app-spec.md) app, built with
Viu components and the reactive core — the sample gallery's showcase of `[Reactive]` source-generated
state, `ReactiveList<T>`, computed views, and typed provide/inject.

## What it shows

- **`[Reactive]` objects** — [`TodoItem`](Todos/TodoItem.cs) is a `[Reactive]` partial class; the
  `Assimalign.Viu.Core` source generator fills in each property's track/trigger plumbing (Vue's
  `reactive()`), so toggling one todo re-renders only what read it.
- **`ReactiveList<T>` + computed views** — [`TodoStore`](Todos/TodoStore.cs) holds a
  `ReactiveList<TodoItem>` and derives the filtered list, remaining/completed counts, and the
  toggle-all state as `Computed<T>` values. The store is a composition-style unit that depends on
  nothing but `Assimalign.Viu.Core`, so it is fully unit-tested with no browser.
- **Component model** — a root [`TodoAppComponent`](Components/TodoAppComponent.cs) renders a keyed
  list of [`TodoItemComponent`](Components/TodoItemComponent.cs) rows (props in, per-row local edit
  state), reused across filtering by `key`.
- **Typed provide/inject** — one `TodoStore` is `provide`d app-wide under a typed `InjectionKey<T>`
  and injected by every component (`TodoStore.Key`).

Every mutation (add, toggle, edit, destroy, toggle-all, clear-completed) and the All/Active/Completed
filters follow the TodoMVC spec. The renderer drives real DOM through the injected node-ops adapter;
there is no ad-hoc JS interop in the sample.

## Run it

```sh
dotnet run --project examples/Assimalign.Viu.TodoMvc
```

Then open the served URL. Build a trimmed publish (what CI's budget gate measures) with:

```sh
dotnet publish examples/Assimalign.Viu.TodoMvc -c Release
```

## Tests

The store and the DOM-drivable component behavior are covered by
[`Assimalign.Viu.TodoMvc.Tests`](../Assimalign.Viu.TodoMvc.Tests) using the in-memory
`Assimalign.Viu.Testing` renderer (no browser).
