namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// The declaration kind of a described <c>@script</c> member ([V01.01.06.11]) — host-neutral, so the
/// language service maps to its LSP completion/symbol kinds at its own edge instead of this library
/// naming an editor enum.
/// </summary>
public enum ScriptDeclaredMemberKind
{
    /// <summary>A field declaration (one entry per declared variable).</summary>
    Field,

    /// <summary>A property declaration.</summary>
    Property,

    /// <summary>A method declaration.</summary>
    Method,
}
