namespace Assimalign.Viu.UtilityCss.VisualStudio;

/// <summary>
/// Names the Visual Studio-owned HTML content type to which the utility-CSS language client may
/// attach.
/// </summary>
/// <remarks>
/// The language-client broker matches through <c>IContentType.IsOfType</c>. The classic
/// <c>HTML</c> type is therefore intentionally absent: <c>LegacyRazor</c> derives from it, and both
/// legacy <c>.cshtml</c> and <c>.razor</c> buffers would activate this client. The modern
/// <c>Razor</c> type is shared by those extensions too. <c>html-delegation</c> is the installed Web
/// Tools top-level type for modern <c>.html</c>/<c>.htm</c> buffers; <c>htmlLSPClient</c> is its
/// contained virtual-document type and is not a host-document activation route.
/// </remarks>
internal static class UtilityCssContentTypes
{
    /// <summary>The modern Web Tools top-level HTML delegation content type.</summary>
    internal const string HtmlDelegation = "html-delegation";
}
