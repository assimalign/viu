using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Declares one component output event on the <c>partial void</c> method that emits it.
/// </summary>
/// <remarks>
/// The attribute is a <b>build-time</b> declaration: the single-file-component source generator reads
/// it out of an <c>@script</c> block, synthesizes the equivalent <see cref="ComponentEvent"/> into
/// the generated partial's <c>IComponentTemplate.Events</c>, and implements the attributed method as
/// a typed <c>IComponentContext.Emit</c> of the declared name, so nothing is discovered by reflection
/// at runtime. Specified by <c>[CMP-30]</c>.
/// <para>
/// A method is the anchor rather than a property because an event carries a <em>payload signature</em>
/// rather than a value: the method's parameter list is what makes the emit call site strongly typed
/// and removes the event-name string literal from the component's own code. The event name is derived
/// from the method name in camel case (<c>Changed</c> → <c>changed</c>) unless <see cref="Name"/>
/// overrides it, and the synthesized declaration validates the emitted argument count.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class EventAttribute : Attribute
{
    /// <summary>Declares the event with its derived name.</summary>
    public EventAttribute()
    {
    }

    /// <summary>Declares the event under an explicit template-facing name.</summary>
    /// <param name="name">The canonical event name, overriding the derived one.</param>
    public EventAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>
    /// Gets or sets the canonical event name, or <see langword="null"/> to derive it from the method
    /// name. Set it when the template spelling cannot be derived — the <c>update:modelValue</c>
    /// two-way-binding convention, for example.
    /// </summary>
    public string? Name { get; set; }
}
