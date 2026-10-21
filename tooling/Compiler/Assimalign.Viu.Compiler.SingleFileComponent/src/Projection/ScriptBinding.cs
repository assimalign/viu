using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// One classified <c>@script</c> member: its declared name and the <see cref="BindingType"/> the
/// render-code-generation path ([V01.01.05.04]/[V01.01.05.05]) reads to decide where a
/// <c>Reference&lt;T&gt;.Value</c> unwrap belongs. One of these per top-level <c>@script</c> member; the
/// collection of them is the binding metadata the template compiler consumes ([SFC-6]).
/// A <see langword="readonly"/> <see langword="record"/> <see langword="struct"/> so
/// it is value-equatable and rides inside the incremental generator's cached model without defeating the
/// cache — the contract the <see cref="EquatableArray{T}"/> it is stored in requires.
/// </summary>
/// <param name="Name">The member name exactly as declared in the <c>@script</c> block.</param>
/// <param name="Type">The classified binding type that drives ref-unwrapping decisions in the template.</param>
public readonly record struct ScriptBinding(string Name, BindingType Type)
{
    /// <summary>Whether access uses the declaring type rather than an instance ([SFC-6]).</summary>
    public bool IsStatic { get; init; }

    /// <summary>The declared delegate shape for a method group; null otherwise ([SFC-CG-12]).</summary>
    public string? HandlerType { get; init; }

    /// <summary>The declared model value type, unwrapping a known reactive reference ([SFC-CG-12]).</summary>
    public string? ModelValueType { get; init; }
}
