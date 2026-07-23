namespace Assimalign.Viu.Components;

/// <summary>Identifies an instance-local component lifecycle phase.</summary>
public enum ComponentLifecycleKind
{
    /// <summary>Before the first rendered subtree is mounted.</summary>
    BeforeMount,

    /// <summary>After the first rendered subtree is mounted.</summary>
    Mounted,

    /// <summary>Before a later rendered subtree is patched.</summary>
    BeforeUpdate,

    /// <summary>After a later rendered subtree is patched.</summary>
    Updated,

    /// <summary>Before teardown starts.</summary>
    BeforeUnmount,

    /// <summary>After teardown completes.</summary>
    Unmounted,

    /// <summary>When an error from a descendant reaches this instance.</summary>
    ErrorCaptured,

    /// <summary>Before server-side rendering serializes the instance.</summary>
    ServerPrefetch,

    /// <summary>When a cached subtree is reactivated.</summary>
    Activated,

    /// <summary>When a cached subtree is deactivated.</summary>
    Deactivated,
}

