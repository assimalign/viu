using System.ComponentModel.Composition;

using Microsoft.VisualStudio.LanguageServer.Client;
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
/// It also derives from <see cref="CodeRemoteContentDefinition.CodeRemoteContentTypeName"/>, the base
/// the editor's language-server parts are registered against. <see cref="ViuLanguageClient"/> starts
/// the server for any buffer of this content type, but completion, hover, outlining, light bulbs, and
/// the Error List entries the server publishes are contributed by parts filtered on that base — a
/// <c>viu</c> content type without it yields a running server whose answers reach no editor surface.
/// The base itself derives from <c>code</c>, so the two are consistent rather than competing. It also
/// pulls in the editor's TextMate content types; those are inert here, because TextMate resolves a
/// grammar by file extension and Visual Studio registers none for <c>.viu</c> — Viu colorization comes
/// from <see cref="ViuClassifierProvider"/>.
/// </para>
/// <para>
/// The binding only takes effect once an editor factory that performs content-type detection owns the
/// file. That is what the <c>.pkgdef</c> shipped beside this assembly arranges — see
/// <c>docs/DESIGN.md</c>, "File extension ownership". Without it the XML editor's wildcard factory
/// claims <c>.viu</c> by sniffing the leading <c>&lt;template&gt;</c> tag and suppresses further
/// detection, and this binding is never consulted.
/// </para>
/// <para>
/// <c>.vue</c> is deliberately not claimed, so no <c>.vue</c> buffer carries this content type and
/// neither the Viu classifier nor <see cref="ViuLanguageClient"/> attaches to one. Visual Studio's Web
/// Tools registers that extension explicitly and taking it over is a separate product decision, not a
/// side effect of adding Viu colorization. The language server's own <c>.vue</c> admission rules
/// remain — they are what the Visual Studio Code client and any other host rely on — but in Visual
/// Studio they are currently unreachable.
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
    [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
    internal static readonly ContentTypeDefinition? ViuContentTypeDefinition = null;

    [Export]
    [FileExtension(ViuFileExtension)]
    [ContentType(Viu)]
    internal static readonly FileExtensionToContentTypeDefinition? ViuFileExtensionDefinition = null;
}
