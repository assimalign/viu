namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>Identifies the supported single-file-component container format.</summary>
/// <remarks>Specified by <c>[SFC-3]</c> and <c>[VUE-1]</c>.</remarks>
public enum SingleFileComponentFormat
{
    /// <summary>The native <c>.viu</c> hybrid container.</summary>
    Viu,

    /// <summary>The documented tag-based <c>.vue</c> compatibility container.</summary>
    Vue
}
