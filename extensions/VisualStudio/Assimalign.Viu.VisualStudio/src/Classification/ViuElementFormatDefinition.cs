using System.ComponentModel.Composition;
using System.Windows.Media;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Default appearance of HTML element tag names in a template.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = ViuClassificationTypeNames.Element)]
[Name(ViuClassificationTypeNames.Element)]
[UserVisible(true)]
[Order(After = Priority.Default)]
internal sealed class ViuElementFormatDefinition : ClassificationFormatDefinition
{
    /// <summary>
    /// Initializes the format with its Viu default. The user's own choice, made through
    /// Tools &gt; Options &gt; Fonts and Colors, overrides everything set here.
    /// </summary>
    public ViuElementFormatDefinition()
    {
        this.DisplayName = "Viu — element tag";
        this.ForegroundColor = Color.FromRgb(0x7E, 0xE7, 0x87);
    }
}
