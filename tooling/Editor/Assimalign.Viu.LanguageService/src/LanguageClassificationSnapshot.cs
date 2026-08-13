using System.Collections.Generic;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Captures semantic classifications together with the exact open-document snapshot they describe.
/// </summary>
/// <param name="Version">
/// The editor-supplied document version, or <see langword="null"/> when the client supplied none.
/// </param>
/// <param name="TextChecksum">
/// The SHA-256 checksum of the UTF-8 document text. Hosts use it to reject a classification response
/// after the editor has already advanced to a newer snapshot.
/// </param>
/// <param name="Classifications">
/// The authored-position C# classifications, preserving the exact classification-type names used by
/// the C# editor. [V01.01.12.07.11]
/// </param>
public sealed record LanguageClassificationSnapshot(
    int? Version,
    string TextChecksum,
    IReadOnlyList<LanguageClassification> Classifications);
