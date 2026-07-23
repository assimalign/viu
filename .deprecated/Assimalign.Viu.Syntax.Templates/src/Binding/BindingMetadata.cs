using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The map from a component member name to its <see cref="BindingType"/>, plus the prop-alias table and the
/// setup-mode flag. The C# port of Vue 3.5's <c>BindingMetadata</c> (<c>@vue/compiler-core</c>
/// <c>options.ts</c>), produced by the component/setup source model and consumed by expression and scope
/// analysis ([V01.01.05.04]).
/// </summary>
/// <remarks>
/// This is transform <i>input</i>, not part of the value-equatable transform output, so it is a plain class
/// rather than a record. It is immutable after construction and safe to share across the single-threaded
/// transform. <see cref="ReportsUnresolvedIdentifiers"/> is a Viu-specific addition with no Vue counterpart:
/// because C# is statically typed and has no <c>Proxy</c> fallback, a template identifier that is neither a
/// template-local, an allowed global, nor a known binding cannot silently resolve to a member — the strict
/// component model sets this flag so such identifiers surface a diagnostic instead of compiling to an invalid
/// member access.
/// </remarks>
public sealed class BindingMetadata
{
    private static readonly IReadOnlyDictionary<string, BindingType> EmptyBindings = new Dictionary<string, BindingType>();

    /// <summary>The empty, permissive metadata used when no component model is supplied.</summary>
    public static readonly BindingMetadata Empty = new();

    private readonly IReadOnlyDictionary<string, BindingType> bindings;
    private readonly IReadOnlyDictionary<string, string>? propertyAliases;

    /// <summary>Creates binding metadata.</summary>
    /// <param name="bindings">The member-name to <see cref="BindingType"/> map, or <see langword="null"/> for none.</param>
    /// <param name="isScriptSetup">
    /// Whether the bindings come from a <c>&lt;script setup&gt;</c> block (upstream <c>__isScriptSetup</c>).
    /// </param>
    /// <param name="reportsUnresolvedIdentifiers">
    /// Whether an identifier absent from <paramref name="bindings"/>, the template-local scope, and the global
    /// allow-list should surface a <see cref="CompilerErrorCode.XViuUnresolvedIdentifier"/> diagnostic (the
    /// strict Viu mode). Defaults to <see langword="false"/> so partial or absent metadata never spuriously
    /// errors and the permissive <c>_ctx.</c> fallback (Vue's behavior) is used instead.
    /// </param>
    /// <param name="propertyAliases">
    /// The map from a <see cref="BindingType.PropertyAliased"/> alias to its real prop name (upstream
    /// <c>__propsAliases</c>), or <see langword="null"/> for none.
    /// </param>
    public BindingMetadata(
        IReadOnlyDictionary<string, BindingType>? bindings = null,
        bool isScriptSetup = false,
        bool reportsUnresolvedIdentifiers = false,
        IReadOnlyDictionary<string, string>? propertyAliases = null)
    {
        this.bindings = bindings ?? EmptyBindings;
        this.propertyAliases = propertyAliases;
        IsScriptSetup = isScriptSetup;
        ReportsUnresolvedIdentifiers = reportsUnresolvedIdentifiers;
    }

    /// <summary>Whether the bindings come from a <c>&lt;script setup&gt;</c> block (upstream <c>__isScriptSetup</c>).</summary>
    public bool IsScriptSetup { get; }

    /// <summary>
    /// Whether unresolved identifiers surface a diagnostic (the strict Viu mode). No Vue counterpart; see the
    /// type remarks.
    /// </summary>
    public bool ReportsUnresolvedIdentifiers { get; }

    /// <summary>Whether <paramref name="name"/> is a known binding.</summary>
    /// <param name="name">The identifier name.</param>
    public bool Contains(string name) => bindings.ContainsKey(name);

    /// <summary>Resolves the <see cref="BindingType"/> of <paramref name="name"/>.</summary>
    /// <param name="name">The identifier name.</param>
    /// <param name="type">The resolved binding type when found.</param>
    /// <returns><see langword="true"/> when <paramref name="name"/> is a known binding.</returns>
    public bool TryGetBindingType(string name, out BindingType type) => bindings.TryGetValue(name, out type);

    /// <summary>Resolves the real prop name for a <see cref="BindingType.PropertyAliased"/> alias.</summary>
    /// <param name="name">The alias name.</param>
    /// <returns>The real prop name, or <see langword="null"/> when no alias is recorded.</returns>
    public string? GetPropertyAlias(string name)
        => propertyAliases is not null && propertyAliases.TryGetValue(name, out var real) ? real : null;
}
