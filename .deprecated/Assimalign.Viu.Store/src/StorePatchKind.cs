namespace Assimalign.Viu.Store;

/// <summary>
/// How a store mutation reached its subscribers — the C# port of Pinia's <c>MutationType</c>
/// (https://pinia.vuejs.org/api/enums/pinia.MutationType.html, <c>packages/pinia/src/types.ts</c>).
/// A <see cref="StoreMutation"/> carries this so a <see cref="Store{TState}.Subscribe"/> callback can
/// tell a direct member write from a grouped <see cref="Store{TState}.Patch(System.Action{TState})"/>.
/// </summary>
public enum StorePatchKind
{
    /// <summary>
    /// A state member was mutated directly (outside <c>Patch</c>) — upstream <c>MutationType.direct</c>.
    /// Delivered through the store's scheduler-batched state watcher, so several direct writes in one
    /// flush coalesce into a single notification.
    /// </summary>
    Direct,

    /// <summary>
    /// The mutation came from the mutator-delegate form
    /// <see cref="Store{TState}.Patch(System.Action{TState})"/>, or from
    /// <see cref="Store{TState}.Reset"/> (which, like Pinia's <c>$reset</c>, applies the initial state
    /// through the mutator form) — upstream <c>MutationType.patchFunction</c>.
    /// </summary>
    PatchFunction,

    /// <summary>
    /// The mutation came from the partial-state form <see cref="Store{TState}.Patch(TState)"/> —
    /// upstream <c>MutationType.patchObject</c>.
    /// </summary>
    PatchObject,
}
