namespace Assimalign.Viu.ServerRenderer;

/// <summary>Reports one successfully emitted document and its original requested route.</summary>
/// <remarks>Reports follow input order and occur only after storage succeeds. Specified by <c>[SSG-5]</c>.</remarks>
public sealed class StaticSiteFile
{
    internal StaticSiteFile(string route, string relativePath, string location)
    {
        Route = route;
        RelativePath = relativePath;
        Location = location;
    }

    /// <summary>Gets the requested route, including the query and fragment used during rendering.</summary>
    public string Route { get; }

    /// <summary>Gets the document's slash-separated path relative to the output root.</summary>
    public string RelativePath { get; }

    /// <summary>Gets the storage location returned by the output after a successful write.</summary>
    public string Location { get; }
}
