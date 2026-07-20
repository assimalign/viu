namespace Assimalign.Viu.Store;

/// <summary>
/// The setup delegate passed to <see cref="Stores.DefineStore{TStore}(string, StoreSetup{TStore})"/> —
/// the C# port of the setup function in Pinia's setup-syntax store
/// <c>defineStore(id, () =&gt; { ... })</c> (https://pinia.vuejs.org/core-concepts/#setup-stores,
/// <c>packages/pinia/src/store.ts</c> <c>createSetupStore</c>). It runs exactly once per
/// <see cref="StoreRegistry"/> the store is resolved in, inside that store's own
/// <see cref="Assimalign.Viu.Reactivity.EffectScope"/>, and returns the store instance — the object
/// whose refs are state, computeds are getters, and methods are actions ([V01.01.09.02] builds those
/// members on top of this delegate).
/// <para>
/// Refs, computeds, watchers, and <c>watchEffect</c>s created while the delegate runs are collected by
/// the store's scope, so disposing the store (or its owning app/registry) tears them all down. Because
/// the delegate is captured at definition time and invoked directly, store construction is fully
/// reflection-free (AOT/trimming-safe): there is no <c>Activator.CreateInstance</c> and no attribute
/// scanning.
/// </para>
/// </summary>
/// <typeparam name="TStore">The store instance type the setup produces.</typeparam>
/// <returns>The newly constructed store instance.</returns>
public delegate TStore StoreSetup<TStore>()
    where TStore : class;
