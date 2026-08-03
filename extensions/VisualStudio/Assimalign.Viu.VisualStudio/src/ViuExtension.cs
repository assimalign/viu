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
        // Opening either container document type loads the extension, as does any solution that
        // opts in through the SDK build property. "viu-vue" is listed because the `.vue` container
        // is a declared compatibility target ([V01.01.06.09]) and a solution holding only `.vue`
        // components would otherwise never activate the language service.
        LoadedWhen = ActivationConstraint.Or(
            ActivationConstraint.EditorContentType("viu"),
            ActivationConstraint.EditorContentType("viu-vue"),
            ActivationConstraint.SolutionHasProjectBuildProperty(
                "ViuVisualStudioLanguageServiceEnabled",
                "^[Tt]rue$")),
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
