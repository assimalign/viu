using System;

namespace Assimalign.Viu.Components;

/// <summary>Registers lifecycle callbacks against one mounted template instance.</summary>
public interface IComponentLifecycle
{
    /// <summary>Registers a callback for one lifecycle phase.</summary>
    /// <param name="kind">The lifecycle phase.</param>
    /// <param name="callback">The instance-local callback.</param>
    void Register(ComponentLifecycleKind kind, Action callback);
}

