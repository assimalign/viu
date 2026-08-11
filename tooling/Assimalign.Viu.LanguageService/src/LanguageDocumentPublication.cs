using System.Collections.Generic;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Captures every expensive semantic result published after one document synchronization round.
/// The shared snapshot lets a host compute classifications and diagnostics from one immutable live
/// document fork, then publish both without mixing document versions. [V01.01.12.07.11]
/// [V01.01.12.07.15]
/// </summary>
/// <param name="ClassificationSnapshot">
/// The versioned semantic classifications when the host requested them; otherwise,
/// <see langword="null"/>.
/// </param>
/// <param name="Diagnostics">
/// The parser, projection, C#, style, and component-contract diagnostics at authored positions.
/// </param>
public sealed record LanguageDocumentPublication(
    LanguageClassificationSnapshot? ClassificationSnapshot,
    IReadOnlyList<LanguageDiagnostic> Diagnostics);
