namespace Assimalign.Viu.LanguageService;

/// <summary>
/// One semantically classified authored span in a Viu single-file-component document.
/// </summary>
/// <param name="Range">
/// The half-open authored range. Generated component scaffold spans are never returned.
/// </param>
/// <param name="ClassificationTypeName">
/// The C# editor classification-type name, such as <c>property name</c> or
/// <c>parameter name</c>.
/// </param>
/// <remarks>
/// Classification names are editor-neutral strings so the language-server host can translate them
/// to its protocol vocabulary without loading editor or Roslyn workspace assemblies into the host.
/// [V01.01.12.07.11]
/// </remarks>
public readonly record struct LanguageClassification(
    LanguageRange Range,
    string ClassificationTypeName);
