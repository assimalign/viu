namespace Assimalign.Viu.State;

/// <summary>
/// Describes how a state-store mutation reached its subscribers, corresponding to Pinia's
/// <c>MutationType</c>.
/// </summary>
public enum StateStorePatchKind
{
    /// <summary>A reactive state member was written outside a grouped patch.</summary>
    Direct,

    /// <summary>
    /// State changed through <see cref="StateStore{TState}.Patch(System.Action{TState})"/> or
    /// <see cref="StateStore{TState}.Reset"/>.
    /// </summary>
    PatchFunction,

    /// <summary>State changed through <see cref="StateStore{TState}.Patch(TState)"/>.</summary>
    PatchObject,
}
