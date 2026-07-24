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
