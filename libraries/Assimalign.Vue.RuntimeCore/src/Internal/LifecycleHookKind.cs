namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// The lifecycle hook slots an instance stores (upstream: the <c>LifecycleHooks</c> enum in
/// <c>packages/runtime-core/src/component.ts</c>).
/// </summary>
internal enum LifecycleHookKind
{
    /// <summary>Before the first render is patched in.</summary>
    BeforeMount,

    /// <summary>After the instance's tree is in the host (post-flush).</summary>
    Mounted,

    /// <summary>Before a re-render patches (pre-patch subtree state observable).</summary>
    BeforeUpdate,

    /// <summary>After a re-render's patches applied (post-flush).</summary>
    Updated,

    /// <summary>Before teardown starts (parent-first).</summary>
    BeforeUnmount,

    /// <summary>After teardown (post-flush, child-first).</summary>
    Unmounted,

    /// <summary>Captures descendant errors (upstream: <c>errorCaptured</c>).</summary>
    ErrorCaptured,

    /// <summary>Awaited by the server renderer before serialization ([V01.01.07.01]).</summary>
    ServerPrefetch,

    /// <summary>Stored for KeepAlive activation ([V01.01.03.18]).</summary>
    Activated,

    /// <summary>Stored for KeepAlive deactivation ([V01.01.03.18]).</summary>
    Deactivated,
}
