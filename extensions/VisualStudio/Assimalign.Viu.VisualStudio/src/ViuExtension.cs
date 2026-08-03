using Microsoft.VisualStudio.Extensibility;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Hosts the Viu language integration in an isolated Visual Studio extension process.
/// </summary>
[VisualStudioContribution]
internal sealed class ViuExtension : Extension
{
    // The extension declares no LoadedWhen constraint, which is deliberate and load-bearing.
    //
    // Nothing static registers the .viu file extension: the packaged extension.vsixmanifest carries no
    // assets, and no pkgdef, editor factory, or grammar claims it. The "viu" and "viu-vue" document
    // types - and therefore the .viu/.vue content-type bindings - come into existence only when this
    // extension loads and its documentTypes contributions are applied at runtime.
    //
    // That makes an editor-content-type activation constraint circular: EditorContentType("viu") can
    // never become true, because the content type it waits on is created by the very load it gates.
    // A solution-build-property term does not rescue it either - gating load on any condition risks
    // the document types registering after a .viu buffer has already been created against the
    // fallback content type, which leaves the document permanently unclassified.
    //
    // Loading unconditionally is cheap here: the extension is hosted out of process, and the language
    // server still performs its own per-document owning-project check before serving anything.

    /// <inheritdoc />
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new(
            id: "Assimalign.Viu.VisualStudio.3c6324dd-5c21-46a2-98d1-6b7b5d701f7c",
            version: this.ExtensionAssemblyVersion,
            publisherName: "Assimalign",
            displayName: "Viu for Visual Studio",
            description: "Editing support for Viu single-file components.")
        {
            Preview = true,
        },
    };
}
