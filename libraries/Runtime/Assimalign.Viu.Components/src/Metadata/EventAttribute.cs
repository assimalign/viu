using System;

namespace Assimalign.Viu.Components;

/// <summary>Declares one generated component output on the partial method that emits it.</summary>
/// <remarks>
/// The source generator writes the corresponding <see cref="ComponentEvent"/> into the static
/// contract and implements the partial method as a typed <see cref="ComponentContext.Emit"/> call.
/// Runtime code performs no attribute discovery. Specified by <c>[CMP-30]</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class EventAttribute : Attribute
{
    /// <summary>Declares an event whose template name is derived from the method name.</summary>
    public EventAttribute()
    {
    }

    /// <summary>Declares an event with an explicit template-facing name.</summary>
    /// <param name="name">The non-empty canonical event name.</param>
    public EventAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>Gets or sets the explicit name, or null to derive it from the method name.</summary>
    public string? Name { get; set; }
}
