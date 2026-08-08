using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// Carries stable projection outputs shared by generators and editor services.
/// </summary>
/// <remarks>
/// Deliberately absent: a compiler-produced style-scope identity. Scoped CSS is deferred until
/// after the component-model arc; reintroducing it is one additive member here plus emission.
/// Specified by <c>[SFC-PIPE-2]</c> and <c>[TOOL-2]</c>.
/// </remarks>
public sealed class SingleFileComponentProjectionResult
{
    /// <summary>Initializes an immutable projection result.</summary>
    public SingleFileComponentProjectionResult(
        string hintName,
        string generatedSource,
        string className,
        string? componentNamespace,
        IEnumerable<SingleFileComponentDiagnostic>? diagnostics = null,
        IEnumerable<SingleFileComponentSourceMapping>? sourceMappings = null)
    {
        HintName = hintName ?? throw new ArgumentNullException(nameof(hintName));
        GeneratedSource = generatedSource ?? throw new ArgumentNullException(nameof(generatedSource));
        ClassName = className ?? throw new ArgumentNullException(nameof(className));
        ComponentNamespace = componentNamespace;
        Diagnostics = diagnostics is null
            ? Array.Empty<SingleFileComponentDiagnostic>()
            : new List<SingleFileComponentDiagnostic>(diagnostics).AsReadOnly();
        SourceMappings = sourceMappings is null
            ? Array.Empty<SingleFileComponentSourceMapping>()
            : new List<SingleFileComponentSourceMapping>(sourceMappings).AsReadOnly();
    }

    /// <summary>Gets the deterministic generator hint name.</summary>
    public string HintName { get; }

    /// <summary>Gets the complete generated C# source.</summary>
    public string GeneratedSource { get; }

    /// <summary>Gets the generated component class name.</summary>
    public string ClassName { get; }

    /// <summary>Gets the generated component namespace.</summary>
    public string? ComponentNamespace { get; }

    /// <summary>Gets stable editor-neutral diagnostics.</summary>
    public IReadOnlyList<SingleFileComponentDiagnostic> Diagnostics { get; }

    /// <summary>Gets source-to-generated mappings.</summary>
    public IReadOnlyList<SingleFileComponentSourceMapping> SourceMappings { get; }
}
