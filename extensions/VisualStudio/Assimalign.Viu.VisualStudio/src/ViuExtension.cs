using Microsoft.VisualStudio.Extensibility;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Hosts the Viu language integration in an isolated Visual Studio extension process.
/// </summary>
[VisualStudioContribution]
internal sealed class ViuExtension : Extension
{
    /// <inheritdoc />
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        // LoadedWhen is emitted by the contribution generator as the command set's supportedScope and
        // nothing else - it never gates the extension parts, the document types, or the language
        // server. Its one job is eager activation: when the scope goes active Visual Studio force
        // loads the command set, which is what starts the out-of-process extension host. With no
        // constraint the scope stays permanently inactive, the host is never started eagerly, and the
        // extension contributes nothing until something else happens to demand it.
        //
        // The two editor-content-type terms can never fire on their own, because nothing static
        // registers the .viu or .vue extension - the packaged vsixmanifest carries no assets, and the
        // content types come into existence only once this extension loads and applies its
        // documentTypes. They are retained because they cost nothing and do fire once a container
        // document is already bound. The two solution-state terms carry the real weight: they are
        // jointly exhaustive, so the host starts eagerly in every session, solution or not.
        LoadedWhen = ActivationConstraint.Or(
            ActivationConstraint.EditorContentType("viu"),
            ActivationConstraint.EditorContentType("viu-vue"),
            ActivationConstraint.SolutionState(SolutionState.NoSolution),
            ActivationConstraint.SolutionState(SolutionState.Exists)),
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
