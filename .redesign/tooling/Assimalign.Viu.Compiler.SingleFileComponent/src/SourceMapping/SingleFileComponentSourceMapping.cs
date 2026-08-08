using System;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>Maps one generated range back to its source range.</summary>
/// <remarks>Specified by <c>[SFC-8]</c>.</remarks>
public sealed class SingleFileComponentSourceMapping
{
    /// <summary>Initializes an immutable source mapping.</summary>
    /// <param name="source">The original source range.</param>
    /// <param name="generated">The generated source range.</param>
    public SingleFileComponentSourceMapping(
        SingleFileComponentSourceRange source,
        SingleFileComponentSourceRange generated)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Generated = generated ?? throw new ArgumentNullException(nameof(generated));
    }

    /// <summary>Gets the original source range.</summary>
    public SingleFileComponentSourceRange Source { get; }

    /// <summary>Gets the generated source range.</summary>
    public SingleFileComponentSourceRange Generated { get; }
}
