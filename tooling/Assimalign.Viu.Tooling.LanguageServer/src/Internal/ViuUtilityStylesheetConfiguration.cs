using Assimalign.Viu.Tooling.UtilityCss;

namespace Assimalign.Viu.Tooling.LanguageServer;

/// <summary>
/// One project utility entry plus its host-resolved reference graph.
/// </summary>
internal sealed record ViuUtilityStylesheetConfiguration(
    string StylesheetText,
    string StylesheetIdentity,
    UtilityStylesheetReferenceGraph ReferenceGraph);
