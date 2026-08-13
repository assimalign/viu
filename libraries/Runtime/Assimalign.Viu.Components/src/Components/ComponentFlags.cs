using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Static input-resolution behavior declared by a component contract.
/// </summary>
/// <remarks>Fallthrough behavior is specified by <c>[CMP-17]</c>.</remarks>
[Flags]
public enum ComponentFlags
{
    /// <summary>No contract-level behavior adjustments.</summary>
    None = 0,

    /// <summary>Undeclared invocation bindings fall through onto the rendered root.</summary>
    InheritFallthroughBindings = 1,
}
