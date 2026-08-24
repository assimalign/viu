namespace Assimalign.Viu.VisualStudio.UtilityCss;

/// <summary>
/// Names the Visual Studio-owned HTML content types to which the utility-CSS language client may
/// attach.
/// </summary>
/// <remarks>
/// At Visual Studio's default settings, a standalone <c>.html</c> or <c>.htm</c> buffer carries the
/// <c>HTML</c> content type. <c>html-delegation</c> identifies Razor-projected HTML buffers and also
/// standalone HTML buffers when the <c>WebTools.Languages.Html.LSP</c> feature flag is enabled. The
/// flag defaults to off, so the client registers both Visual Studio-owned types. Because
/// <c>LegacyRazor</c> derives from <c>HTML</c>, the client middle layer separately rejects document
/// requests whose URI is not an HTML document.
/// </remarks>
internal static class UtilityCssContentTypes
{
    /// <summary>The standalone HTML document content type used at default settings.</summary>
    internal const string Html = "HTML";

    /// <summary>The Razor projection and feature-flag-enabled HTML delegation content type.</summary>
    internal const string HtmlDelegation = "html-delegation";
}
