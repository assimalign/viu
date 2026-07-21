using System;


namespace Assimalign.Viu;

/// <summary>
/// A template-ref binding — the C# port of a normalized template ref in <c>@vue/runtime-core</c>
/// (<c>packages/runtime-core/src/rendererTemplateRef.ts</c>,
/// https://vuejs.org/guide/essentials/template-refs.html). A binding is exactly one of:
/// <list type="bullet">
/// <item>a reactive ref-object (<see cref="IReference{T}"/> of <see cref="object"/>) that receives
/// the mounted element or a component's exposed surface — nulled on unmount;</item>
/// <item>a function ref (<see cref="Action{T}"/> of <see cref="object"/>) invoked with the
/// element/instance on mount and <c>null</c> on unmount, enabling custom collection (the v-for ref
/// pattern).</item>
/// </list>
/// Vue's string-ref form — which resolves against a component instance proxy — is intentionally not
/// ported: this model is reflection-free and has no string case. The renderer applies a binding
/// through its <c>SetReference</c> in the post-flush phase (<see cref="Renderer{TNode}"/>).
/// Element refs carry <see cref="object"/> at this TNode-generic layer (the boxed platform node);
/// platform wrappers give typed access.
/// </summary>
public readonly struct TemplateReference : IEquatable<TemplateReference>
{
    private readonly IReference<object?>? _reference;
    private readonly Action<object?>? _function;

    private TemplateReference(IReference<object?>? reference, Action<object?>? function)
    {
        _reference = reference;
        _function = function;
    }

    /// <summary>Creates a ref-object binding (upstream: a <c>Ref</c> template ref).</summary>
    /// <param name="reference">The reactive ref that receives the element or exposed surface.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public static TemplateReference FromReference(IReference<object?> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new TemplateReference(reference, null);
    }

    /// <summary>Creates a function-ref binding (upstream: a function template ref).</summary>
    /// <param name="function">Invoked with the element/instance on mount and null on unmount.</param>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> is null.</exception>
    public static TemplateReference FromFunction(Action<object?> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        return new TemplateReference(null, function);
    }

    /// <summary>Whether this binding is a ref-object (as opposed to a function ref).</summary>
    public bool IsReferenceObject => _reference is not null;

    /// <summary>The ref-object when <see cref="IsReferenceObject"/>; otherwise null.</summary>
    internal IReference<object?>? ReferenceObject => _reference;

    /// <summary>The function when this binding is a function ref; otherwise null.</summary>
    internal Action<object?>? Function => _function;

    /// <summary>
    /// Classifies a raw <c>"ref"</c> prop value into a binding, or null when there is none. A value
    /// that is neither an <see cref="IReference{T}"/> of <see cref="object"/> nor an
    /// <see cref="Action{T}"/> of <see cref="object"/> is reported (upstream dev warning for an
    /// invalid ref type) and treated as no ref.
    /// </summary>
    /// <param name="raw">The raw <c>"ref"</c> prop value.</param>
    internal static TemplateReference? FromRaw(object? raw) => raw switch
    {
        null => null,
        IReference<object?> reference => new TemplateReference(reference, null),
        Action<object?> function => new TemplateReference(null, function),
        _ => Invalid(raw),
    };

    /// <inheritdoc />
    public bool Equals(TemplateReference other)
        => ReferenceEquals(_reference, other._reference) && Equals(_function, other._function);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TemplateReference other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_reference, _function);

    /// <summary>Whether two bindings wrap the same ref-object or the same function.</summary>
    /// <param name="left">The left binding.</param>
    /// <param name="right">The right binding.</param>
    public static bool operator ==(TemplateReference left, TemplateReference right) => left.Equals(right);

    /// <summary>Whether two bindings differ.</summary>
    /// <param name="left">The left binding.</param>
    /// <param name="right">The right binding.</param>
    public static bool operator !=(TemplateReference left, TemplateReference right) => !left.Equals(right);

    private static TemplateReference? Invalid(object raw)
    {
        RuntimeWarnings.Warn(
            $"Invalid template ref of type {raw.GetType().Name}: a template ref must be an "
            + "IReference<object?> or an Action<object?> (string refs are not supported).");
        return null;
    }
}
