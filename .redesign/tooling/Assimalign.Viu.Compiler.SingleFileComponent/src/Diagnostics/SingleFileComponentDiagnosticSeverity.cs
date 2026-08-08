namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>Identifies editor-neutral diagnostic severity.</summary>
/// <remarks>Specified by <c>[SFC-DIAG-1]</c> and <c>[TOOL-5]</c>.</remarks>
public enum SingleFileComponentDiagnosticSeverity
{
    /// <summary>Informational guidance.</summary>
    Information,

    /// <summary>A recoverable issue that should be surfaced to the developer.</summary>
    Warning,

    /// <summary>An issue that prevents a valid projection.</summary>
    Error
}
