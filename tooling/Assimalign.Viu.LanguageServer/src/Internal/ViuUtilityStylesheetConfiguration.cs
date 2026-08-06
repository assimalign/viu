using Assimalign.Viu.UtilityCss;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// One project utility entry plus its host-resolved reference graph.
/// </summary>
internal sealed record ViuUtilityStylesheetConfiguration(
    string StylesheetText,
    string StylesheetIdentity,
    UtilityStylesheetReferenceGraph ReferenceGraph);
