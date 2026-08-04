using System.ComponentModel.Composition;
using System.Windows.Media;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Default appearance of the tags Viu itself defines: <c>template</c>, <c>slot</c>, <c>style</c>, <c>script</c>, and the legacy <c>@template</c>/<c>@style</c> block headers.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = ViuClassificationTypeNames.Tag)]
[Name(ViuClassificationTypeNames.Tag)]
[UserVisible(true)]
[Order(After = Priority.Default)]
internal sealed class ViuTagFormatDefinition : ClassificationFormatDefinition
{
    /// <summary>
    /// Initializes the format with its Viu default. The user's own choice, made through
    /// Tools &gt; Options &gt; Fonts and Colors, overrides everything set here.
    /// </summary>
    public ViuTagFormatDefinition()
    {
        this.DisplayName = "Viu — framework tag";
        this.ForegroundColor = Color.FromRgb(0x56, 0x9C, 0xD6);
    }
}
