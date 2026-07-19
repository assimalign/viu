namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// Selects which <see cref="IDirective"/> hook the renderer invokes, so hook dispatch is an
/// allocation-free enum switch rather than a per-call delegate (upstream: the string
/// <c>name</c> passed to <c>invokeDirectiveHook</c>).
/// </summary>
internal enum DirectiveHookKind
{
    /// <summary>Before the element's attributes/listeners are applied.</summary>
    Created,

    /// <summary>Before the element is inserted.</summary>
    BeforeMount,

    /// <summary>After the element and subtree are mounted (post-flush).</summary>
    Mounted,

    /// <summary>Before the element updates.</summary>
    BeforeUpdate,

    /// <summary>After the element and its children updated (post-flush).</summary>
    Updated,

    /// <summary>Before the element is removed.</summary>
    BeforeUnmount,

    /// <summary>After the element is removed (post-flush).</summary>
    Unmounted,
}
