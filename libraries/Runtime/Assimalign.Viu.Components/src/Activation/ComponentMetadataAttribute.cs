using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Records a generated component's registered name for compile-time tooling. Runtime activation uses
/// explicit registrations, never attribute inspection. Specified by <c>[CMP-35]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ComponentMetadataAttribute : Attribute
{
    /// <summary>Creates component metadata with its exact ordinal registration name.</summary>
    /// <param name="name">The nonempty registration name.</param>
    public ComponentMetadataAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>Gets the exact registration name generated for this component.</summary>
    public string Name { get; }
}
