namespace Assimalign.Viu.Store;

/// <summary>
/// A store action-subscription callback — the C# port of the callback passed to Pinia's
/// <c>store.$onAction(context =&gt; { ... })</c> (https://pinia.vuejs.org/core-concepts/actions.html#Subscribing-to-actions,
/// <c>packages/pinia/src/store.ts</c>). It is invoked before an action body runs (for every action
/// routed through <see cref="Store{TState}.RunAction(string, System.Action)"/> and its overloads) and
/// may register <see cref="StoreActionContext.After"/> / <see cref="StoreActionContext.OnError"/>
/// hooks on the supplied <paramref name="context"/> to observe the action's resolved result or thrown
/// exception.
/// </summary>
/// <param name="context">The invocation context: action name, owning store, and the after/error hooks.</param>
public delegate void StoreActionCallback(StoreActionContext context);
