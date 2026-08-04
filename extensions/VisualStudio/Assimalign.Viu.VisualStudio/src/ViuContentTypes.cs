using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Registers the <c>viu</c> content type and binds the <c>.viu</c> file extension to it.
/// </summary>
/// <remarks>
/// <para>
/// The content type derives from <c>code</c>, which is what makes the editor treat a Viu container as
/// source rather than prose: word wrap, brace matching, and the outlining and classification services
/// all key off that base.
/// </para>
/// <para>
/// The binding only takes effect once an editor factory that performs content-type detection owns the
/// file. That is what the <c>.pkgdef</c> shipped beside this assembly arranges — see
/// <c>docs/DESIGN.md</c>, "File extension ownership". Without it the XML editor's wildcard factory
/// claims <c>.viu</c> by sniffing the leading <c>&lt;template&gt;</c> tag and suppresses further
/// detection, and this binding is never consulted.
/// </para>
/// <para>
/// <c>.vue</c> is deliberately not claimed. Visual Studio's Web Tools registers that extension
/// explicitly, and taking it over is a separate product decision rather than a side effect of adding
/// Viu colorization; <c>.vue</c> support stays at the language-server level.
/// </para>
/// </remarks>
internal static class ViuContentTypes
{
    /// <summary>The content type name for a Viu single-file component.</summary>
    public const string Viu = "viu";

    /// <summary>The canonical Viu single-file-component file extension.</summary>
    public const string ViuFileExtension = ".viu";

    [Export]
    [Name(Viu)]
    [BaseDefinition("code")]
    internal static readonly ContentTypeDefinition? ViuContentTypeDefinition = null;

    [Export]
    [FileExtension(ViuFileExtension)]
    [ContentType(Viu)]
    internal static readonly FileExtensionToContentTypeDefinition? ViuFileExtensionDefinition = null;
}
