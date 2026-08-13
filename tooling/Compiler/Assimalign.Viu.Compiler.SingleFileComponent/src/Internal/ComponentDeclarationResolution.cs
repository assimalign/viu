namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>The compile-time outcome of resolving an authored component tag.</summary>
internal enum ComponentDeclarationResolution
{
    /// <summary>No statically readable component answers to the authored tag.</summary>
    Missing,

    /// <summary>More than one statically readable component answers to the authored tag.</summary>
    Ambiguous,

    /// <summary>Exactly one statically readable component answers to the authored tag.</summary>
    Resolved,
}
