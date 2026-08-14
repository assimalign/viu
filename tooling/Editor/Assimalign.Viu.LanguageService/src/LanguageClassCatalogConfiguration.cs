using System;
using System.Collections.Generic;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// An immutable host-provided snapshot of the class-catalog JSON documents visible to one project.
/// </summary>
/// <remarks>
/// The constructor copies the supplied list, so a configuration instance is a stable cache key.
/// Hosts should reuse that instance while their selected catalog files are unchanged and replace it
/// after any discovery or file-stamp change. [V01.01.12.30]
/// </remarks>
public sealed class LanguageClassCatalogConfiguration
{
    /// <summary>Creates one immutable class-catalog snapshot.</summary>
    /// <param name="catalogJsonDocuments">
    /// The catalog JSON documents in deterministic merge order. A null element is rejected; an
    /// unreadable file should be omitted by the host.
    /// </param>
    public LanguageClassCatalogConfiguration(
        IReadOnlyList<string> catalogJsonDocuments)
    {
        ArgumentNullException.ThrowIfNull(catalogJsonDocuments);

        var documents = new string[catalogJsonDocuments.Count];
        for (var index = 0; index < catalogJsonDocuments.Count; index++)
        {
            documents[index] = catalogJsonDocuments[index] ??
                throw new ArgumentException(
                    "A class-catalog JSON document cannot be null.",
                    nameof(catalogJsonDocuments));
        }

        CatalogJsonDocuments = Array.AsReadOnly(documents);
    }

    /// <summary>
    /// Gets the catalog JSON documents in deterministic merge order. The returned collection is
    /// immutable for the lifetime of this configuration.
    /// </summary>
    public IReadOnlyList<string> CatalogJsonDocuments { get; }
}
