using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Declares one component input parameter on the property that receives its value.
/// </summary>
/// <remarks>
/// The attribute is a <b>build-time</b> declaration: the single-file-component source generator reads
/// it out of an <c>@script</c> block and synthesizes the equivalent
/// <see cref="ComponentParameter"/> into the generated partial's
/// <c>IComponentTemplate.Parameters</c>, so nothing is discovered by reflection at runtime and the
/// declaration stays trimming- and WASM/NativeAOT-safe. Specified by <c>[CMP-26]</c>.
/// <para>
/// The template-facing argument name is derived from the property name in camel case
/// (<c>Title</c> → <c>title</c>, <c>ModelValue</c> → <c>modelValue</c>, <c>URL</c> → <c>url</c>)
/// unless <see cref="Name"/> overrides it; the runtime still accepts the kebab-case spelling of the
/// derived name, exactly as for an imperative declaration [CMP-13]. Specified by <c>[CMP-27]</c>.
/// </para>
/// <para>
/// The property's initial value — its initializer, or the type's default when it has none — is
/// captured once per mounted instance and restored whenever the parent supplies no argument, which
/// is how an attribute-declared parameter expresses a default without a factory delegate. Specified
/// by <c>[CMP-29]</c>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ParameterAttribute : Attribute
{
    /// <summary>Declares the parameter with its derived name.</summary>
    public ParameterAttribute()
    {
    }

    /// <summary>Declares the parameter under an explicit template-facing name.</summary>
    /// <param name="name">The canonical argument name, overriding the derived one.</param>
    public ParameterAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>
    /// Gets or sets the canonical argument name, or <see langword="null"/> to derive it from the
    /// property name. Set it when the template spelling cannot be derived — a namespaced name such
    /// as <c>update:modelValue</c>, for example.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets whether the caller must supply the parameter. A usage that omits it is a build
    /// error wherever the usage is statically resolvable ([SFC-USE-3]) and a development warning at
    /// mount otherwise [CMP-12]. The C# <c>required</c> modifier on the property declares the same
    /// thing. Specified by <c>[CMP-28]</c>.
    /// </summary>
    public bool IsRequired { get; set; }
}
