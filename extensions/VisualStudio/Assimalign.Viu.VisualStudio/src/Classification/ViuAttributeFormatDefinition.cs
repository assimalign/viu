using System.ComponentModel.Composition;
using System.Windows.Media;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Default appearance of plain attribute names in a template, and property names in a style block.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = ViuClassificationTypeNames.Attribute)]
[Name(ViuClassificationTypeNames.Attribute)]
[UserVisible(true)]
[Order(After = Priority.Default)]
internal sealed class ViuAttributeFormatDefinition : ClassificationFormatDefinition
{
    /// <summary>
    /// Initializes the format with its Viu default. The user's own choice, made through
    /// Tools &gt; Options &gt; Fonts and Colors, overrides everything set here.
    /// </summary>
    public ViuAttributeFormatDefinition()
    {
        this.DisplayName = "Viu — attribute";
        this.ForegroundColor = Color.FromRgb(0x9C, 0xDC, 0xFE);
    }
}
