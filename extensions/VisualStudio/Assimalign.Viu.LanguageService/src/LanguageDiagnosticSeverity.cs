namespace Assimalign.Viu.LanguageService;

/// <summary>The editor-facing severity of a language diagnostic.</summary>
public enum LanguageDiagnosticSeverity
{
    /// <summary>An error that prevents the authored construct from being understood.</summary>
    Error = 1,

    /// <summary>A potential problem that does not prevent parsing.</summary>
    Warning = 2,

    /// <summary>Informational guidance.</summary>
    Information = 3,

    /// <summary>A low-priority hint.</summary>
    Hint = 4,
}
